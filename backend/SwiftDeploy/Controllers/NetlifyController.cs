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
            var redirectUri = _config["Netlify:RedirectUri"];
            
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, "Netlify");

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
                    functions_dir = "netlify/functions"
                }
            };

            // Create site
            var createSiteResp = await client.PostAsJsonAsync("https://api.netlify.com/api/v1/sites", sitePayload);
            var siteResponseBody = await createSiteResp.Content.ReadAsStringAsync();

            if (!createSiteResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Site creation failed", error = siteResponseBody });

            var siteData = JsonDocument.Parse(siteResponseBody);
            var siteId = siteData.RootElement.GetProperty("id").GetString();
            var siteUrl = siteData.RootElement.GetProperty("url").GetString();

            // --------- GitHub API client for committing netlify.toml -----------
            var githubClient = new HttpClient();
            githubClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            githubClient.DefaultRequestHeaders.UserAgent.ParseAdd("SwiftDeployApp"); // Required by GitHub API

            // Step 1: Get latest commit SHA on branch
            var refResp = await githubClient.GetAsync($"https://api.github.com/repos/{request.Repo}/git/ref/heads/{request.Branch}");
            if (!refResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to get branch ref from GitHub", error = await refResp.Content.ReadAsStringAsync() });

            var refData = JsonDocument.Parse(await refResp.Content.ReadAsStringAsync());
            string latestCommitSha = refData.RootElement.GetProperty("object").GetProperty("sha").GetString();

            // Step 2: Get tree SHA of latest commit
            var commitResp = await githubClient.GetAsync($"https://api.github.com/repos/{request.Repo}/git/commits/{latestCommitSha}");
            if (!commitResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to get commit info from GitHub", error = await commitResp.Content.ReadAsStringAsync() });

            var commitData = JsonDocument.Parse(await commitResp.Content.ReadAsStringAsync());
            string treeSha = commitData.RootElement.GetProperty("tree").GetProperty("sha").GetString();

            // Step 3: Create a new blob for netlify.toml content
            string netlifyTomlContent = @"
                [build]
                  base = "".""
                  publish = "".""
                  functions = ""netlify/functions""
                ";
            // to be replaced in the future with a proper template engine
            var blobPayload = new
            {
                content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(netlifyTomlContent)),
                encoding = "base64"
            };

            var blobResp = await githubClient.PostAsJsonAsync($"https://api.github.com/repos/{request.Repo}/git/blobs", blobPayload);
            if (!blobResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to create blob for netlify.toml", error = await blobResp.Content.ReadAsStringAsync() });

            var blobData = JsonDocument.Parse(await blobResp.Content.ReadAsStringAsync());
            string blobSha = blobData.RootElement.GetProperty("sha").GetString();

            // Step 4: Create new tree with netlify.toml update
            var newTreePayload = new
            {
                base_tree = treeSha,
                tree = new[]
                {
                    new
                    {
                        path = "netlify.toml",
                        mode = "100644",
                        type = "blob",
                        sha = blobSha
                    }
                }
            };

            var treeResp = await githubClient.PostAsJsonAsync($"https://api.github.com/repos/{request.Repo}/git/trees", newTreePayload);
            if (!treeResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to create new tree for netlify.toml update", error = await treeResp.Content.ReadAsStringAsync() });

            var treeData = JsonDocument.Parse(await treeResp.Content.ReadAsStringAsync());
            string newTreeSha = treeData.RootElement.GetProperty("sha").GetString();

            // Step 5: Create new commit referencing new tree and latest commit parent
            var newCommitPayload = new
            {
                message = "Add netlify.toml to configure build and publish settings",
                tree = newTreeSha,
                parents = new[] { latestCommitSha }
            };

            var newCommitResp = await githubClient.PostAsJsonAsync($"https://api.github.com/repos/{request.Repo}/git/commits", newCommitPayload);
            if (!newCommitResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to create new commit for netlify.toml", error = await newCommitResp.Content.ReadAsStringAsync() });

            var newCommitData = JsonDocument.Parse(await newCommitResp.Content.ReadAsStringAsync());
            string newCommitSha = newCommitData.RootElement.GetProperty("sha").GetString();

            // Step 6: Update branch reference to new commit SHA
            var updateRefPayload = new { sha = newCommitSha };
            var updateRefResp = await githubClient.PatchAsJsonAsync($"https://api.github.com/repos/{request.Repo}/git/refs/heads/{request.Branch}", updateRefPayload);
            if (!updateRefResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to update GitHub branch reference with new commit", error = await updateRefResp.Content.ReadAsStringAsync() });

            // ------------- Trigger Netlify build --------------
            var buildResp = await client.PostAsync($"https://api.netlify.com/api/v1/sites/{siteId}/builds", null);
            var buildBody = await buildResp.Content.ReadAsStringAsync();

            if (!buildResp.IsSuccessStatusCode)
                return BadRequest(new { message = "Build trigger failed", error = buildBody });

            return Ok(new
            {
                message = "Deployment started with committed netlify.toml!",
                site_id = siteId,
                site_url = siteUrl,
                build_response = JsonSerializer.Deserialize<object>(buildBody)
            });
        }
    }

}
