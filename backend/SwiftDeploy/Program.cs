using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SwiftDeploy.Controllers;
using SwiftDeploy.Data;
using SwiftDeploy.Services;
using SwiftDeploy.Services.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor(); //there was an error in netlify login so i added this line
builder.Services.AddScoped<ITemplateEngine, TemplateEngine>();
builder.Services.AddScoped<JwtHelper>();// In Program.cs or Startup.cs
builder.Services.AddScoped<IUnifiedDeploymentService, UnifiedDeploymentService>();// In Program.cs or Startup.cs
//builder.Services.AddScoped<ITokenService, TokenService>();
// Authentication registration (fixed: single AddAuthentication with chained handlers)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
    options.Cookie.Path = "/";
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
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
        options.Scope.Add("user:email");

        options.SaveTokens = true;

        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.IsEssential = true;
        options.CorrelationCookie.Path = "/";

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");

        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var accessToken = context.AccessToken;
                context.Response.Cookies.Append("GitHubAccessToken", accessToken, new CookieOptions
                {
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    IsEssential = true,
                    Path = "/"
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
                var redirectUri = "http://localhost:5173/auth-callback";
                context.Response.Redirect(redirectUri);
                context.HandleResponse();
            }
        };
    })
    .AddOAuth("Netlify", options =>
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
                var redirectUri = "http://localhost:5173/netlify-callback";
                context.Response.Redirect(redirectUri);
                context.HandleResponse();
            }
        };
    });

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
        return mongoClient.GetDatabase("SwiftDeploy");
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

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

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseCookiePolicy();
    app.UseAuthentication();
    app.UseAuthorization();

    // Debug: log all registered endpoints to help find ambiguous routes
    var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Registered endpoints:");
    foreach (var endpoint in endpointDataSource.Endpoints)
    {
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            var methods = routeEndpoint.Metadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods ?? new[] { "ANY" };
            logger.LogInformation(" {Methods}  {RoutePattern}  => {DisplayName}",
                string.Join(",", methods), routeEndpoint.RoutePattern.RawText, routeEndpoint.DisplayName);
        }
        else
        {
            logger.LogInformation(" {Endpoint}", endpoint.DisplayName ?? endpoint.ToString());
        }
    }
    app.MapControllers();
    app.Run();
