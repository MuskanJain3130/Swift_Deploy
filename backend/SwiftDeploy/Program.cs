using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SwiftDeploy.Controllers;
using SwiftDeploy.Data;
using SwiftDeploy.Services;
using SwiftDeploy.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITemplateEngine, TemplateEngine>();
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IUnifiedDeploymentService, UnifiedDeploymentService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<JwtHelper>(); // Add this line with your other services

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
{
    // This is the crucial part for a decoupled frontend
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
    // Ensure the cookie path is root to be accessible everywhere
    options.Cookie.Path = "/";
})
.AddJwtBearer("JWT", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
})
.AddOAuth("GitHub", options =>
{
    options.ClientId = builder.Configuration["GitHub:ClientId"];
    options.ClientSecret = builder.Configuration["GitHub:ClientSecret"];
    options.CallbackPath = new PathString("/api/auth/github/callback");

    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
    options.UserInformationEndpoint = "https://api.github.com/user";

    options.Scope.Add("read:user");
    options.Scope.Add("repo");

    options.SaveTokens = true;

    // *** THIS IS THE CRUCIAL ADDITION FOR CORRELATION COOKIE ***
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.IsEssential = true; // Mark as essential if needed
    options.CorrelationCookie.Path = "/"; // Ensure the correlation cookie is also at the root path

    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");

    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;
            // Set a cookie with the token
            // Consider making this HttpOnly = true if the frontend doesn't need to read it directly via JS
            // and you pass it via the URL or another secure mechanism.
            context.Response.Cookies.Append("GitHubAccessToken", accessToken, new CookieOptions
            {
                HttpOnly = false, // Set to true for security, but then JS can't read it
                Secure = true,
                SameSite = SameSiteMode.None, // Also apply SameSite=None here
                IsEssential = true, // Mark as essential
                Path = "/" // Ensure this cookie is also set at the root path
            });

            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            request.Headers.Add("User-Agent", "SwiftDeployApp");

            var response = await context.Backchannel.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            context.RunClaimActions(user.RootElement);
        },
        OnTicketReceived = async context =>
        {
            // Get the access token and the original redirect URI
            var accessToken = context.Properties.GetTokenValue("access_token");
            var redirectUri = "http://localhost:5173/auth-callback";

            // Log everything to the console
            Console.WriteLine("--- OnTicketReceived DEBUG LOG ---");
            Console.WriteLine($"Original Redirect URI: {redirectUri}");
            Console.WriteLine($"Access Token: {accessToken}");

            // Construct the final redirect URL
            string finalRedirectUrl = redirectUri;
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(redirectUri))
            {
                // Append the token to the redirect URI as a query parameter
                // This is how your frontend (e.g., React app) will receive the token.
                finalRedirectUrl = $"{redirectUri}";
            }
            Console.WriteLine($"Final Redirect URL: {finalRedirectUrl}");

            // This is the core fix. We manually handle the redirect after the middleware
            // has finished processing the ticket. This ensures the token is in the URL.
            context.Response.Redirect(finalRedirectUrl);
            context.HandleResponse(); // Stop the default middleware redirect behavior
        }
    };
}).AddOAuth("Netlify", options =>
{
    options.ClientId = builder.Configuration["Netlify:ClientId"];
    options.ClientSecret = builder.Configuration["Netlify:ClientSecret"];
    options.CallbackPath = new PathString("/api/auth/netlify/callback");

    options.AuthorizationEndpoint = "https://app.netlify.com/authorize";
    options.TokenEndpoint = "https://api.netlify.com/oauth/token";
    options.UserInformationEndpoint = "https://api.netlify.com/api/v1/user";

    options.SaveTokens = true;

    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.IsEssential = true;
    options.CorrelationCookie.Path = "/";

    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "full_name");
    options.ClaimActions.MapJsonKey("urn:netlify:email", "email");

    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;
            context.Response.Cookies.Append("NetlifyAccessToken", accessToken, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.None,
                IsEssential = true,
                Path = "/"
            });

            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

            var response = await context.Backchannel.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            context.RunClaimActions(user.RootElement);
        },
        OnTicketReceived = async context =>
        {
            var redirectUri = "http://localhost:5173/netlify-callback";//change
            context.Response.Redirect(redirectUri);
            context.HandleResponse();
        }
    };
}).AddOAuth("Vercel", options =>
{
    options.ClientId = builder.Configuration["Vercel:ClientId"];
    options.ClientSecret = builder.Configuration["Vercel:ClientSecret"];
    options.CallbackPath = new PathString("/api/auth/vercel/callback");

    options.AuthorizationEndpoint = "https://vercel.com/oauth/authorize";
    options.TokenEndpoint = "https://api.vercel.com/v2/oauth/access_token";
    options.UserInformationEndpoint = "https://api.vercel.com/v2/user";

    options.Scope.Add("all"); // request full access

    options.SaveTokens = true;

    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");

    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

            var response = await context.Backchannel.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            context.RunClaimActions(user);
        }
    };
}); ;

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var mongoClient = sp.GetRequiredService<IMongoClient>();
    return mongoClient.GetDatabase("SwiftDeploy"); // change to your DB name
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Cookie Policy middleware configuration
builder.Services.AddCookiePolicy(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.OnAppendCookie = cookieContext =>
    {
        cookieContext.CookieOptions.SameSite = SameSiteMode.None;
        cookieContext.CookieOptions.Secure = true;
        cookieContext.CookieOptions.IsEssential = true;
    };
    options.OnDeleteCookie = cookieContext =>
    {
        cookieContext.CookieOptions.SameSite = SameSiteMode.None;
        cookieContext.CookieOptions.Secure = true;
        cookieContext.CookieOptions.IsEssential = true;
    };
});

var app = builder.Build();

app.UseCors(builder =>
    builder.WithOrigins("http://localhost:5173", "https://localhost:5174") // Add https origin for frontend if applicable
           .AllowAnyHeader()
           .AllowAnyMethod()
           .AllowCredentials());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection(); // Ensure this is active and you're using HTTPS in development
app.UseRouting();

// Order is important: UseCookiePolicy before UseAuthentication
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();