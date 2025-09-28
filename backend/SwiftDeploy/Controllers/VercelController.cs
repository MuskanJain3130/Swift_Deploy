using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Net.Http.Headers;

namespace SwiftDeploy.Controllers
{
    [Route("api/vercel")]
    [ApiController]
    public class VercelController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public VercelController(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        // STEP 1: Redirect user to Vercel login
        [HttpGet("login")]
        public IActionResult Login()
        {
            var clientId = _config["Vercel:ClientId"];
            var redirectUri = _config["Vercel:RedirectUri"];

            var authUrl = $"https://vercel.com/oauth/authorize?client_id={clientId}&redirect_uri={redirectUri}&scope=all";
            return Redirect(authUrl);
        }

        // STEP 2: Callback after login
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code)
        {
            var clientId = _config["Vercel:ClientId"];
            var clientSecret = _config["Vercel:ClientSecret"];
            var redirectUri = _config["Vercel:RedirectUri"];

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("code", code),
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret),
                new KeyValuePair<string,string>("redirect_uri", redirectUri)
            });

            var response = await _httpClient.PostAsync("https://api.vercel.com/v2/oauth/access_token", content);
            var rawResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return BadRequest(new { error = "Failed to exchange code", body = rawResponse });

            var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(rawResponse);
            var accessToken = tokenData?["access_token"]?.ToString();

            // Save in cookies
            Response.Cookies.Append("VercelAccessToken", accessToken!, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(30)
            });

            return Ok(new { message = "Vercel connected successfully", token = accessToken });
        }

        // STEP 3: Deploy GitHub repo to Vercel
        [HttpPost("deploy")]
        public async Task<IActionResult> Deploy([FromBody] DeployRequest request)
        {
            var vercelToken = Request.Cookies["VercelAccessToken"];
            if (string.IsNullOrEmpty(vercelToken))
                return Unauthorized(new { error = "No Vercel token found" });

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", vercelToken);

            var payload = new
            {
                name = $"swiftdeploy-{Guid.NewGuid().ToString()[..8]}",
                gitRepository = new
                {
                    type = "github",
                    repo = request.Repo,  // "username/repo"
                    ref_ = request.Branch // branch name
                }
            };

            var deployResponse = await client.PostAsJsonAsync("https://api.vercel.com/v13/deployments", payload);
            var body = await deployResponse.Content.ReadAsStringAsync();

            if (!deployResponse.IsSuccessStatusCode)
                return BadRequest(new { error = "Deployment failed", body });

            var data = JsonSerializer.Deserialize<object>(body);
            return Ok(new { message = "Deployment started!", response = data });
        }
    }

    public class DeployRequest
    {
        public string Repo { get; set; } = string.Empty;
        public string Branch { get; set; } = "main";
    }
}

