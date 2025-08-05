//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Authentication.OAuth;
//using System.Security.Claims;
//using System.Text.Json;

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddAuthentication(options =>
//{
//    //options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//    options.DefaultChallengeScheme = "GitHub";
//})
//.AddCookie()
//.AddOAuth("GitHub", options =>
//{
//    options.ClientId = builder.Configuration["GitHub:ClientId"];
//    options.ClientSecret = builder.Configuration["GitHub:ClientSecret"];
//    options.CallbackPath = new PathString("/api/auth/github/callback");

//    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
//    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
//    options.UserInformationEndpoint = "https://api.github.com/user";

//    options.Scope.Add("read:user");
//    options.Scope.Add("repo"); // for private repos if needed

//    options.SaveTokens = true;

//    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
//    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
//    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");

//    options.Events = new OAuthEvents
//    {
//        OnCreatingTicket = async context =>
//        {
//            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
//            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
//            request.Headers.Add("User-Agent", "SwiftDeployApp");

//            var response = await context.Backchannel.SendAsync(request);
//            response.EnsureSuccessStatusCode();

//            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
//            context.RunClaimActions(user.RootElement);
//        },
//        OnTicketReceived = async context =>
//        {
//            // Add a debugger breakpoint here, or a console log.
//            // This is the most likely place the token is null.
//            var accessToken = context.Properties.GetTokenValue("access_token");

//            Console.WriteLine($"--- OnTicketReceived Event ---");
//            Console.WriteLine($"Access Token: {accessToken}");

//            if (string.IsNullOrEmpty(accessToken))
//            {
//                Console.WriteLine("Error: Access token is NULL or EMPTY!");
//            }

//            var redirectUri = context.Properties.RedirectUri;

//            var finalRedirectUrl = $"{redirectUri}?token={accessToken}";
//            context.Response.Redirect(finalRedirectUrl);
//            context.HandleResponse();
//        }

//    };
//});

//builder.Services.AddControllers();
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//var app = builder.Build();

//app.UseCors(builder =>
//    builder.WithOrigins("http://localhost:5173")
//           .AllowAnyHeader()
//           .AllowAnyMethod()
//           .AllowCredentials());

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseAuthentication();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();




//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Authentication.OAuth;
//using System.Security.Claims;
//using System.Text.Json;
//using SwiftDeploy.Controllers;

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//    options.DefaultChallengeScheme = "GitHub";
//})
//.AddCookie(options =>
//{
//    // This is the crucial part for a decoupled frontend
//    options.Cookie.SameSite = SameSiteMode.None;
//    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//    options.Cookie.IsEssential = true;
//})
//.AddOAuth("GitHub", options =>
//{
//    options.ClientId = builder.Configuration["GitHub:ClientId"];
//    options.ClientSecret = builder.Configuration["GitHub:ClientSecret"];
//    options.CallbackPath = new PathString("/api/auth/github/callback");

//    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
//    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
//    options.UserInformationEndpoint = "https://api.github.com/user";

//    options.Scope.Add("read:user");
//    options.Scope.Add("repo");

//    options.SaveTokens = true;

//    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
//    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
//    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");

//    options.Events = new OAuthEvents
//    {
//        OnCreatingTicket = async context =>
//        {
//            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
//            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
//            request.Headers.Add("User-Agent", "SwiftDeployApp");

//            var response = await context.Backchannel.SendAsync(request);
//            response.EnsureSuccessStatusCode();

//            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
//            context.RunClaimActions(user.RootElement);
//        },
//        OnTicketReceived = async context =>
//        {
//            // Get the access token and the original redirect URI
//            var accessToken = context.Properties.GetTokenValue("access_token");
//            var redirectUri = context.Properties.RedirectUri;

//            // Log everything to the console
//            Console.WriteLine("--- OnTicketReceived DEBUG LOG ---");
//            Console.WriteLine($"Original Redirect URI: {redirectUri}");
//            Console.WriteLine($"Access Token: {accessToken}");

//            // Construct the final redirect URL
//            string finalRedirectUrl = redirectUri;
//            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(redirectUri))
//            {
//                finalRedirectUrl = $"{redirectUri}?token={accessToken}";
//            }
//            Console.WriteLine($"Final Redirect URL: {finalRedirectUrl}");

//            // This is the core fix. We manually handle the redirect after the middleware
//            // has finished processing the ticket. This ensures the token is in the URL.
//            context.Response.Redirect(finalRedirectUrl);
//            context.HandleResponse(); // Stop the default middleware redirect behavior
//        }
//    };
//});

//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//var app = builder.Build();

//app.UseCors(builder =>
//    builder.WithOrigins("http://localhost:5173")
//           .AllowAnyHeader()
//           .AllowAnyMethod()
//           .AllowCredentials());

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

////app.UseHttpsRedirection();
//app.UseAuthentication();
//app.UseAuthorization();
//app.MapControllers();

//app.Run();





using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(options =>
{
    // The default scheme is still needed for any cookie-based auth you might add later
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(builder =>
    builder.WithOrigins("http://localhost:5173")
           .AllowAnyHeader()
           .AllowAnyMethod()
           .AllowCredentials());

//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();