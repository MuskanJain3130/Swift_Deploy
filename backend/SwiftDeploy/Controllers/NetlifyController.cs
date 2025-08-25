using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using SwiftDeploy.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace SwiftDeploy.Controllers
{
    [Route("api")]
    [ApiController]
    public class NetlifyAuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public NetlifyAuthController(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        [HttpGet("auth/netlify/login")]
        public IActionResult Login()
        {
            //var clientId = _config["Netlify:ClientId"];
            var redirectUri = _config["Netlify:RedirectUri"];
            //var authUrl = $"https://app.netlify.com/authorize?client_id={clientId}&response_type=code&redirect_uri={redirectUri}";
             // If none is provided, use a default.
            //string redirectUri = "http://localhost:5173/auth-callback";
            // The Challenge method will save this RedirectUri in the state parameter.
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, "Netlify");

            //return Redirect(authUrl);
        }


        [HttpGet("netlify/callback")]
        public async Task<IActionResult> Callback(string code)
        {
            var clientId = _config["Netlify:ClientId"];
            var clientSecret = _config["Netlify:ClientSecret"];
            var redirectUri = _config["Netlify:RedirectUri"];

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "authorization_code"),
                new KeyValuePair<string,string>("code", code),
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret),
                new KeyValuePair<string,string>("redirect_uri", redirectUri)
            });

            var tokenResponse = await _httpClient.PostAsync("https://api.netlify.com/oauth/token", content);

            var rawResponse = await tokenResponse.Content.ReadAsStringAsync();
            Console.WriteLine("--- Netlify Token Exchange Debug ---");
            Console.WriteLine($"Status: {tokenResponse.StatusCode}");
            Console.WriteLine($"Body: {rawResponse}");

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return BadRequest(new
                {
                    error = "Token exchange failed",
                    status = tokenResponse.StatusCode,
                    body = rawResponse
                });
            }

            var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(rawResponse);

            if (tokenData == null || !tokenData.ContainsKey("access_token"))
                return BadRequest("Failed to get Netlify token");

            var accessToken = tokenData["access_token"]?.ToString();

            Response.Cookies.Append("NetlifyAccessToken", accessToken!, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(30)
            });

            return Ok(new { message = "Netlify connected successfully!", token = accessToken });
        }

        [HttpGet("netlify/sites")]
        public async Task<IActionResult> GetSites()
        {
            var accessToken = Request.Cookies["NetlifyAccessToken"];
            if (string.IsNullOrEmpty(accessToken))
                return Unauthorized(new { error = "No Netlify token found." });

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var res = await client.GetAsync("https://api.netlify.com/api/v1/sites");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, new { error = body });

            // Deserialize before returning
            var sites = JsonSerializer.Deserialize<object>(body);
            return Ok(sites);
        }

        [HttpPost("netlify/deploy")]
        public async Task<IActionResult> CreateSiteAndDeploy([FromBody] DeployRequest request)
        {
            // 🔑 Netlify PAT (store securely in env vars ideally)
            var netlifyToken = Request.Cookies["NetlifyAccessToken"];
            if (string.IsNullOrEmpty(netlifyToken))
                return Unauthorized(new { error = "Netlify token not found. Please login first." });

            var githubToken = Request.Cookies["GitHubAccessToken"];
            if (string.IsNullOrEmpty(githubToken))
                return Unauthorized(new { error = "GitHub token not found. Please login first." });

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", netlifyToken);

            var sitePayload = new
            {
                name = $"swiftdeploy-{Guid.NewGuid().ToString().Substring(0, 8)}",
                repo = new
                {
                    provider = "github",
                    repo = request.Repo, // "username/repo"
                    branch = request.Branch
                },
                build_settings = new
                {
                    @base = "/", 
                    publish="/",// Sets the base directory
                    functions_dir = "netlify/functions" // Sets the functions directory
                }
            };

            var createSiteResp = await client.PostAsJsonAsync("https://api.netlify.com/api/v1/sites", sitePayload);
            var siteResponseBody = await createSiteResp.Content.ReadAsStringAsync();

            if (!createSiteResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Site creation failed", error = siteResponseBody });
            Console.WriteLine(siteResponseBody);
            var siteData = JsonDocument.Parse(siteResponseBody);
            var siteId = siteData.RootElement.GetProperty("id").GetString();
            var siteUrl = siteData.RootElement.GetProperty("url").GetString();

            // ✅ Kick off build
            var buildResp = await client.PostAsync($"https://api.netlify.com/api/v1/sites/{siteId}/builds", null);
            var buildBody = await buildResp.Content.ReadAsStringAsync();

            if (!buildResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Build trigger failed", error = buildBody });

            return Ok(new
            {
                message = "Deployment started!",
                site_id = siteId,
                site_url = siteUrl,
                build_response = JsonSerializer.Deserialize<object>(buildBody)
            });
        }
        //public async Task<IActionResult> CreateSiteAndDeploy([FromBody] DeployRequest request)
        //{
        //    var token = Request.Cookies["NetlifyAccessToken"];
        //    if (string.IsNullOrEmpty(token))
        //        return Unauthorized(new { error = "Netlify token not found. Please login first." });

        //    using var client = new HttpClient();
        //    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        //    // 1. Create a new site
        //    var sitePayload = new
        //    {
        //        name = $"swiftdeploy-{Guid.NewGuid():N}".Substring(0, 16),
        //        build_settings = new
        //        {
        //            repo_url = $"https://github.com/{request.Repo}",
        //            provider = "github",
        //            repo_branch = request.Branch ?? "main"
        //        }
        //    };

        //    var createSiteResp = await client.PostAsJsonAsync("https://api.netlify.com/api/v1/sites", sitePayload);
        //    var siteResponseBody = await createSiteResp.Content.ReadAsStringAsync();

        //    if (!createSiteResp.IsSuccessStatusCode)
        //        return BadRequest(new { message = "Site creation failed", error = siteResponseBody });

        //    var siteData = JsonDocument.Parse(siteResponseBody);
        //    var siteId = siteData.RootElement.GetProperty("id").GetString();

        //    // 2. Trigger build
        //    var buildResp = await client.PostAsync($"https://api.netlify.com/api/v1/sites/{siteId}/builds", null);
        //    var buildBody = await buildResp.Content.ReadAsStringAsync();

        //    if (!buildResp.IsSuccessStatusCode)
        //        return BadRequest(new { message = "Build trigger failed", error = buildBody });

        //    return Ok(new
        //    {
        //        message = "Deployment started!",
        //        site_id = siteId,
        //        site_url = siteData.RootElement.GetProperty("url").GetString(),
        //        build_response = JsonSerializer.Deserialize<object>(buildBody)
        //    });
        //}

    }

}
