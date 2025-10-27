////using Microsoft.AspNetCore.Authentication;
////using Microsoft.AspNetCore.Mvc;
////using SwiftDeploy.Models;
////using System.Net.Http.Headers;
////using System.Text.Json;

////namespace SwiftDeploy.Controllers
////{
////    [Route("api")]
////    [ApiController]
////    public class VercelAuthController : ControllerBase
////    {
////        private readonly IConfiguration _config;
////        private readonly IHttpClientFactory _httpClientFactory;

////        public VercelAuthController(IConfiguration config, IHttpClientFactory httpClientFactory)
////        {
////            _config = config;
////            _httpClientFactory = httpClientFactory;
////        }

////        // -----------------------------------------------------------
////        // STEP 1: Redirect user to Vercel login page
////        // -----------------------------------------------------------
////        [HttpGet("auth/vercel/login")]
////        public IActionResult Login()
////        {
////            Console.WriteLine("Vercel Login endpoint hit ✅");
////            // Redirect to Vercel OAuth using the ASP.NET middleware
////            var redirectUri = _config["Vercel:RedirectUri"];
////            // e.g. http://localhost:5173/vercel-callback

////            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, "Vercel");
////        }

////        // -----------------------------------------------------------
////        // STEP 2: The callback URL (handled automatically by middleware)
////        // This endpoint itself usually won’t be hit directly,
////        // but keeping it for debugging and completeness.
////        // -----------------------------------------------------------
////        [HttpGet("/auth/vercel/callback")]
////        public IActionResult Callback()
////        {
////            return Ok(new { message = "Vercel authentication successful! Redirecting..." });
////        }

////        // -----------------------------------------------------------
////        // STEP 3: Fetch current Vercel user info (using saved access token)
////        // -----------------------------------------------------------
////        [HttpGet("vercel/user")]
////        public async Task<IActionResult> GetUserInfo()
////        {
////            var token = Request.Cookies["VercelAccessToken"];
////            if (string.IsNullOrEmpty(token))
////                return Unauthorized(new { error = "Vercel access token not found. Please login first." });

////            var client = _httpClientFactory.CreateClient();
////            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

////            var response = await client.GetAsync("https://api.vercel.com/v2/user");
////            var body = await response.Content.ReadAsStringAsync();

////            if (!response.IsSuccessStatusCode)
////                return StatusCode((int)response.StatusCode, new { error = body });

////            var user = JsonSerializer.Deserialize<object>(body);
////            return Ok(user);
////        }

////        // -----------------------------------------------------------
////        // STEP 4: Deploy a GitHub repository to Vercel
////        // (trigger one-click deployment)
////        // -----------------------------------------------------------
////        [HttpPost("vercel/deploy")]
////        public async Task<IActionResult> DeployProject([FromBody] DeployRequest request)
////        {
////            var token = Request.Cookies["VercelAccessToken"];
////            if (string.IsNullOrEmpty(token))
////                return Unauthorized(new { error = "Vercel token missing — please login again." });

////            var githubToken = Request.Cookies["GitHubAccessToken"];
////            if (string.IsNullOrEmpty(githubToken))
////                return Unauthorized(new { error = "GitHub token missing — please login again." });

////            var client = _httpClientFactory.CreateClient();
////            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

////            // Build the Vercel deployment payload
////            var deployPayload = new
////            {
////                name = $"swiftdeploy-{Guid.NewGuid().ToString()[..8]}",
////                gitSource = new
////                {
////                    type = "github",
////                    repoId = request.Repo, // "username/repo"
////                    refName = request.Branch // branch name
////                }
////            };

////            var response = await client.PostAsJsonAsync("https://api.vercel.com/v13/deployments", deployPayload);
////            var raw = await response.Content.ReadAsStringAsync();

////            if (!response.IsSuccessStatusCode)
////                return BadRequest(new { message = "Vercel deployment failed", error = raw });

////            var json = JsonDocument.Parse(raw);
////            var deploymentUrl = json.RootElement.GetProperty("url").GetString();

////            return Ok(new
////            {
////                message = "Deployment initiated successfully!",
////                url = $"https://{deploymentUrl}"
////            });
////        }
////    }    
////}using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc;
//using Octokit;
//using Octokit.Internal;
//using System.Text;
//using System.Text.Json;

//namespace SwiftDeploys.Controllers
//{
//    [ApiController]
//    [Route("api/auth/vercel")]
//    public class VercelAuthController : ControllerBase
//    {
//        private readonly IConfiguration _config;
//        private readonly IHttpClientFactory _httpClientFactory;
//        private readonly ILogger<VercelAuthController> _logger;

//        public VercelAuthController(
//            IConfiguration config,
//            IHttpClientFactory httpClientFactory,
//            ILogger<VercelAuthController> logger)
//        {
//            _config = config;
//            _httpClientFactory = httpClientFactory;
//            _logger = logger;
//        }

//        // Changed to POST to avoid CORS issues with redirects
//        [HttpPost("login")]
//        public IActionResult Login()
//        {
//            //var returnUrl = request?.ReturnUrl;
//            var clientId = _config["Vercel:ClientId"];
//            var redirectUri = $"http://localhost:5173/vercel-callback";

//            _logger.LogInformation("=== Vercel OAuth Login ===");
//            _logger.LogInformation($"Client ID: {clientId}");
//            _logger.LogInformation($"Redirect URI: {redirectUri}");
//            //_logger.LogInformation($"Return URL: {returnUrl}");

//            if (string.IsNullOrEmpty(clientId))
//            {
//                _logger.LogError("Client ID is missing!");
//                return BadRequest(new { error = "Client ID not configured" });
//            }

//            var state = Guid.NewGuid().ToString();

//            // Store state, return URL, and flow type
//            Response.Cookies.Append("vercel_oauth_state", state, new CookieOptions
//            {
//                HttpOnly = true,
//                Secure = true,
//                SameSite = SameSiteMode.Lax,
//                MaxAge = TimeSpan.FromMinutes(10)
//            });

//            Response.Cookies.Append("vercel_flow_type", "user_login", new CookieOptions
//            {
//                HttpOnly = true,
//                Secure = true,
//                SameSite = SameSiteMode.Lax,
//                MaxAge = TimeSpan.FromMinutes(10)
//            });

//            //if (!string.IsNullOrEmpty(returnUrl))
//            //{
//            //    Response.Cookies.Append("vercel_return_url", returnUrl, new CookieOptions
//            //    {
//            //        HttpOnly = true,
//            //        Secure = true,
//            //        SameSite = SameSiteMode.Lax,
//            //        MaxAge = TimeSpan.FromMinutes(10)
//            //    });
//            //}

//            var authUrl = $"https://vercel.com/oauth/authorize" +
//                $"?client_id={Uri.EscapeDataString(clientId)}" +
//                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
//                $"&response_type=code" +
//                $"&scope=user" +
//                $"&state={Uri.EscapeDataString(state)}";

//            _logger.LogInformation($"Auth URL generated: {authUrl}");

//            // Return the URL instead of redirecting
//            return Ok(new { redirectUrl = authUrl });
//        }

//        // Unified callback - handles BOTH flows
//        [HttpGet("callback")]
//        public async Task<IActionResult> Callback(
//            [FromQuery] string? code,
//            [FromQuery] string? configurationId,
//            [FromQuery] string? next,
//            [FromQuery] string? teamId,
//            [FromQuery] string? state,
//            [FromQuery] string? error,
//            [FromQuery] string? source)
//        {
//            _logger.LogInformation("=== Vercel Callback ===");
//            _logger.LogInformation($"Code: {!string.IsNullOrEmpty(code)}");
//            _logger.LogInformation($"ConfigurationId: {configurationId}");
//            _logger.LogInformation($"TeamId: {teamId}");
//            _logger.LogInformation($"State: {state}");
//            _logger.LogInformation($"Next: {next}");
//            _logger.LogInformation($"Source: {source}");

//            // Determine flow type
//            var flowType = Request.Cookies["vercel_flow_type"];
//            var isUserLogin = flowType == "user_login";
//            var returnUrl = Request.Cookies["vercel_return_url"] ?? "http://localhost:5173/vercel-callback";

//            _logger.LogInformation($"Flow: {(isUserLogin ? "User Login" : "Integration Install")}");

//            if (!string.IsNullOrEmpty(error))
//            {
//                _logger.LogError($"Error: {error}");
//                CleanupCookies();
//                return Redirect($"{returnUrl}?error={error}");
//            }

//            if (string.IsNullOrEmpty(code))
//            {
//                _logger.LogError("No code received");
//                CleanupCookies();
//                return Redirect($"{returnUrl}?error=no_code");
//            }

//            // Validate state for user login
//            if (isUserLogin && !string.IsNullOrEmpty(state))
//            {
//                var storedState = Request.Cookies["vercel_oauth_state"];
//                if (state != storedState)
//                {
//                    _logger.LogWarning("State validation failed!");
//                    CleanupCookies();
//                    return Redirect($"{returnUrl}?error=invalid_state");
//                }
//            }

//            try
//            {
//                var clientId = _config["Vercel:ClientId"];
//                var clientSecret = _config["Vercel:ClientSecret"];
//                var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/vercel/callback";

//                // Exchange code for token
//                var tokenRequest = new Dictionary<string, string>
//                {
//                    { "client_id", clientId },
//                    { "client_secret", clientSecret },
//                    { "code", code },
//                    { "redirect_uri", redirectUri }
//                };

//                var client = _httpClientFactory.CreateClient();
//                var response = await client.PostAsync(
//                    "https://api.vercel.com/v2/oauth/access_token",
//                    new FormUrlEncodedContent(tokenRequest)
//                );

//                var responseContent = await response.Content.ReadAsStringAsync();
//                _logger.LogInformation($"Token response: {response.StatusCode}");
//                _logger.LogInformation($"Token response body: {responseContent}");

//                if (!response.IsSuccessStatusCode)
//                {
//                    _logger.LogError($"Token exchange failed: {responseContent}");
//                    CleanupCookies();
//                    return Redirect($"{returnUrl}?error=token_failed");
//                }

//                var tokenData = JsonSerializer.Deserialize<JsonElement>(responseContent);
//                var accessToken = tokenData.GetProperty("access_token").GetString();
//                var tokenTeamId = tokenData.TryGetProperty("team_id", out var tid) ? tid.GetString() : null;
//                var userId = tokenData.TryGetProperty("user_id", out var uid) ? uid.GetString() : null;

//                _logger.LogInformation("✅ Token received successfully");

//                // Get user info - Fixed to handle proper response structure
//                string? username = null;
//                string? email = null;
//                string? name = null;

//                try
//                {
//                    var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.vercel.com/v2/user");
//                    userRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
//                    var userResponse = await client.SendAsync(userRequest);

//                    if (userResponse.IsSuccessStatusCode)
//                    {
//                        var userContent = await userResponse.Content.ReadAsStringAsync();
//                        _logger.LogInformation($"User info response: {userContent}");

//                        var userData = JsonSerializer.Deserialize<JsonElement>(userContent);

//                        // Vercel API returns user data directly at root level, not nested
//                        username = userData.TryGetProperty("username", out var un) ? un.GetString() : null;
//                        email = userData.TryGetProperty("email", out var em) ? em.GetString() : null;
//                        name = userData.TryGetProperty("name", out var nm) ? nm.GetString() : null;

//                        _logger.LogInformation($"Parsed user info - Username: {username}, Email: {email}, Name: {name}");
//                    }
//                    else
//                    {
//                        var errorContent = await userResponse.Content.ReadAsStringAsync();
//                        _logger.LogWarning($"User info request failed: {userResponse.StatusCode} - {errorContent}");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning($"Could not fetch user info: {ex.Message}");
//                }

//                // Store all data in cookies
//                StoreCookie("VercelAccessToken", accessToken);

//                if (!string.IsNullOrEmpty(teamId ?? tokenTeamId))
//                    StoreCookie("VercelTeamId", teamId ?? tokenTeamId);

//                if (!string.IsNullOrEmpty(userId))
//                    StoreCookie("VercelUserId", userId);

//                if (!string.IsNullOrEmpty(username))
//                    StoreCookie("VercelUsername", username);

//                if (!string.IsNullOrEmpty(email))
//                    StoreCookie("VercelEmail", email);

//                if (!string.IsNullOrEmpty(name))
//                    StoreCookie("VercelName", name);

//                if (!string.IsNullOrEmpty(configurationId))
//                    StoreCookie("VercelConfigId", configurationId);

//                CleanupCookies();

//                // Handle redirects based on flow type
//                if (isUserLogin)
//                {
//                    _logger.LogInformation($"User login complete → {returnUrl}");
//                    return Redirect($"{returnUrl}?success=true");
//                }
//                else if (!string.IsNullOrEmpty(next))
//                {
//                    _logger.LogInformation($"Integration install → redirecting to Vercel");
//                    return Redirect(next);
//                }
//                else
//                {
//                    return Redirect("http://localhost:5173/vercel-callback?success=true");
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Exception during callback");
//                CleanupCookies();
//                return Redirect($"{returnUrl}?error=exception");
//            }
//        }

//        // Get current user info
//        [HttpGet("me")]
//        public IActionResult GetCurrentUser()
//        {
//            var token = Request.Cookies["VercelAccessToken"];

//            if (string.IsNullOrEmpty(token))
//            {
//                return Ok(new { authenticated = false });
//            }

//            return Ok(new
//            {
//                authenticated = true,
//                token = token,
//                userId = Request.Cookies["VercelUserId"],
//                username = Request.Cookies["VercelUsername"],
//                email = Request.Cookies["VercelEmail"],
//                name = Request.Cookies["VercelName"],
//                teamId = Request.Cookies["VercelTeamId"],
//                configId = Request.Cookies["VercelConfigId"]
//            });
//        }

//        // Logout
//        [HttpPost("logout")]
//        public IActionResult Logout()
//        {
//            Response.Cookies.Delete("VercelAccessToken");
//            Response.Cookies.Delete("VercelTeamId");
//            Response.Cookies.Delete("VercelUserId");
//            Response.Cookies.Delete("VercelUsername");
//            Response.Cookies.Delete("VercelEmail");
//            Response.Cookies.Delete("VercelName");
//            Response.Cookies.Delete("VercelConfigId");

//            return Ok(new { success = true });
//        }

//        // Return integration URL instead of redirecting
//        [HttpPost("install-integration")]
//        public IActionResult InstallIntegration()
//        {
//            // Replace 'swift-deploy' with your integration slug
//            var integrationUrl = "https://vercel.com/integrations/swift-deploy";

//            _logger.LogInformation($"Integration URL: {integrationUrl}");

//            // Return URL instead of redirecting to avoid CORS
//            return Ok(new { redirectUrl = integrationUrl });
//        }

//        // Helper methods
//        private void StoreCookie(string name, string value)
//        {
//            Response.Cookies.Append(name, value, new CookieOptions
//            {
//                HttpOnly = false,
//                Secure = true,
//                SameSite = SameSiteMode.None,
//                MaxAge = TimeSpan.FromDays(30),
//                Path = "/"
//            });
//        }

//        private void CleanupCookies()
//        {
//            Response.Cookies.Delete("vercel_oauth_state");
//            Response.Cookies.Delete("vercel_return_url");
//            Response.Cookies.Delete("vercel_flow_type");
//        }
//    }

//    // Request models
//    public class LoginRequest
//    {
//        public string? ReturnUrl { get; set; }
//    }
//}

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/vercel")]
public class VercelController : ControllerBase
{
    private const string BaseApiUrl = "https://api.vercel.com/v9";
    private readonly ILogger<VercelController> _logger;

    public VercelController(ILogger<VercelController> logger)
    {
        _logger = logger;
    }

    // Generate a unique project name using repo, branch, and timestamp
    private string GenerateProjectName(string owner, string repo, string branch)
    {
        string raw = $"{owner}-{repo}-{branch}".ToLower();

        // Vercel project names must be lowercase alphanumeric with hyphens
        // Remove special characters and replace with hyphens
        raw = System.Text.RegularExpressions.Regex.Replace(raw, "[^a-z0-9-]", "-");
        raw = System.Text.RegularExpressions.Regex.Replace(raw, "-+", "-"); // Remove duplicate hyphens
        raw = raw.Trim('-'); // Remove leading/trailing hyphens

        // Add short hash for uniqueness
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(raw + DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLower();

        var projectName = $"{raw}-{hex.Substring(0, 8)}";

        // Vercel project names have a max length of 52 characters
        if (projectName.Length > 52)
        {
            projectName = projectName.Substring(0, 52).TrimEnd('-');
        }

        return projectName;
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate(
        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
        [FromBody] VercelRequestData requestData
    )
    {
        _logger.LogInformation("=== Vercel Deployment Initiation ===");

        if (string.IsNullOrEmpty(apiToken))
        {
            _logger.LogError("Missing Vercel API token");
            return BadRequest(new { error = "Missing Vercel API token in headers." });
        }

        if (requestData == null || string.IsNullOrEmpty(requestData.Owner) ||
            string.IsNullOrEmpty(requestData.RepoName) ||
            string.IsNullOrEmpty(requestData.Branch))
        {
            _logger.LogError("Missing required fields");
            return BadRequest(new { error = "Missing required information: Owner, RepoName, or Branch." });
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        try
        {
            // 1. Get user/team info to determine team ID (if applicable)
            _logger.LogInformation("Fetching user information...");
            var userResponse = await client.GetAsync("https://api.vercel.com/v2/user");
            var userString = await userResponse.Content.ReadAsStringAsync();

            if (!userResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to get user info: {userString}");
                return StatusCode((int)userResponse.StatusCode, new { error = "Failed to authenticate with Vercel", details = userString });
            }

            var userJson = JsonDocument.Parse(userString);
            var userId = userJson.RootElement.TryGetProperty("user", out var user)
                ? user.GetProperty("id").GetString()
                : userJson.RootElement.GetProperty("id").GetString();

            _logger.LogInformation($"User ID: {userId}");

            // 2. Generate project name automatically
            string projectName = GenerateProjectName(requestData.Owner, requestData.RepoName, requestData.Branch);
            _logger.LogInformation($"Generated project name: {projectName}");

            // 3. Create project payload
            var githubRepo = $"{requestData.Owner}/{requestData.RepoName}";

            var createPayload = new
            {
                name = projectName,
                framework = requestData.Framework ?? DetectFramework(requestData.BuildCommand),
                buildCommand = requestData.BuildCommand ?? "",
                outputDirectory = requestData.BuildDir ?? "out",
                installCommand = requestData.InstallCommand ?? "npm install",
                gitRepository = new
                {
                    type = "github",
                    repo = githubRepo
                },
                environmentVariables = requestData.EnvironmentVariables ?? new object[] { }
            };

            _logger.LogInformation($"Creating project: {projectName}");
            _logger.LogInformation($"GitHub Repo: {githubRepo}");
            _logger.LogInformation($"Branch: {requestData.Branch}");

            // Determine if we're using a team
            var createProjectUrl = !string.IsNullOrEmpty(requestData.TeamId)
                ? $"{BaseApiUrl}/projects?teamId={requestData.TeamId}"
                : $"{BaseApiUrl}/projects";

            var createContent = new StringContent(
                JsonSerializer.Serialize(createPayload),
                Encoding.UTF8,
                "application/json"
            );

            var createResponse = await client.PostAsync(createProjectUrl, createContent);
            var createResult = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to create project: {createResult}");
                return StatusCode((int)createResponse.StatusCode, new
                {
                    error = "Failed to create Vercel project",
                    details = createResult
                });
            }

            var projectData = JsonDocument.Parse(createResult);
            var projectId = projectData.RootElement.GetProperty("id").GetString();

            _logger.LogInformation($"✅ Project created successfully: {projectId}");

            // 4. Link GitHub repository
            _logger.LogInformation("Linking GitHub repository...");
            var linkUrl = !string.IsNullOrEmpty(requestData.TeamId)
                ? $"{BaseApiUrl}/projects/{projectId}/link?teamId={requestData.TeamId}"
                : $"{BaseApiUrl}/projects/{projectId}/link";

            var linkPayload = new
            {
                type = "github",
                repo = githubRepo,
                gitBranch = requestData.Branch
            };

            var linkContent = new StringContent(
                JsonSerializer.Serialize(linkPayload),
                Encoding.UTF8,
                "application/json"
            );

            var linkResponse = await client.PostAsync(linkUrl, linkContent);
            var linkResult = await linkResponse.Content.ReadAsStringAsync();

            if (!linkResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to link GitHub (may already be linked): {linkResult}");
            }
            else
            {
                _logger.LogInformation("✅ GitHub repository linked");
            }

            // 5. Trigger initial deployment
            _logger.LogInformation("Triggering deployment...");
            var deployUrl = !string.IsNullOrEmpty(requestData.TeamId)
                ? $"https://api.vercel.com/v13/deployments?teamId={requestData.TeamId}"
                : "https://api.vercel.com/v13/deployments";

            var deployPayload = new
            {
                name = projectName,
                project = projectId,
                gitSource = new
                {
                    type = "github",
                    repo = githubRepo,
                    @ref = requestData.Branch
                },
                target = "production"
            };

            var deployContent = new StringContent(
                JsonSerializer.Serialize(deployPayload),
                Encoding.UTF8,
                "application/json"
            );

            var deployResponse = await client.PostAsync(deployUrl, deployContent);
            var deployResult = await deployResponse.Content.ReadAsStringAsync();

            if (!deployResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Deployment may be triggered automatically: {deployResult}");
            }
            else
            {
                _logger.LogInformation("✅ Deployment triggered");
            }

            // Parse deployment data
            object deploymentData = null;
            try
            {
                deploymentData = JsonSerializer.Deserialize<object>(deployResult);
            }
            catch
            {
                deploymentData = new { message = "Deployment will be triggered automatically by Vercel" };
            }

            return Ok(new
            {
                success = true,
                projectId = projectId,
                projectName = projectName,
                githubRepo = githubRepo,
                branch = requestData.Branch,
                projectUrl = $"https://vercel.com/{userId}/{projectName}",
                deploymentUrl = $"https://{projectName}.vercel.app",
                project = JsonSerializer.Deserialize<object>(createResult),
                deployment = deploymentData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Vercel deployment");
            return StatusCode(500, new
            {
                error = "Internal server error",
                message = ex.Message,
                details = ex.ToString()
            });
        }
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(
        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
        [FromQuery] string? teamId = null
    )
    {
        if (string.IsNullOrEmpty(apiToken))
            return BadRequest(new { error = "Missing Vercel API token" });

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var url = !string.IsNullOrEmpty(teamId)
            ? $"{BaseApiUrl}/projects?teamId={teamId}"
            : $"{BaseApiUrl}/projects";

        var response = await client.GetAsync(url);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, result);

        return Ok(JsonSerializer.Deserialize<object>(result));
    }

    [HttpDelete("projects/{projectId}")]
    public async Task<IActionResult> DeleteProject(
        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
        string projectId,
        [FromQuery] string? teamId = null
    )
    {
        if (string.IsNullOrEmpty(apiToken))
            return BadRequest(new { error = "Missing Vercel API token" });

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var url = !string.IsNullOrEmpty(teamId)
            ? $"{BaseApiUrl}/projects/{projectId}?teamId={teamId}"
            : $"{BaseApiUrl}/projects/{projectId}";

        var response = await client.DeleteAsync(url);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, result);

        return Ok(new { success = true, message = "Project deleted successfully" });
    }

    [HttpGet("deployments/{projectId}")]
    public async Task<IActionResult> GetDeployments(
        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
        string projectId,
        [FromQuery] string? teamId = null
    )
    {
        if (string.IsNullOrEmpty(apiToken))
            return BadRequest(new { error = "Missing Vercel API token" });

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var url = !string.IsNullOrEmpty(teamId)
            ? $"https://api.vercel.com/v6/deployments?projectId={projectId}&teamId={teamId}"
            : $"https://api.vercel.com/v6/deployments?projectId={projectId}";

        var response = await client.GetAsync(url);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, result);

        return Ok(JsonSerializer.Deserialize<object>(result));
    }

    // Helper method to detect framework from build command
    private string DetectFramework(string? buildCommand)
    {
        if (string.IsNullOrEmpty(buildCommand))
            return null;

        buildCommand = buildCommand.ToLower();

        if (buildCommand.Contains("next")) return "nextjs";
        if (buildCommand.Contains("vite")) return "vite";
        if (buildCommand.Contains("react")) return "create-react-app";
        if (buildCommand.Contains("vue")) return "vue";
        if (buildCommand.Contains("nuxt")) return "nuxtjs";
        if (buildCommand.Contains("gatsby")) return "gatsby";
        if (buildCommand.Contains("svelte")) return "svelte";
        if (buildCommand.Contains("astro")) return "astro";

        return null;
    }
}

public class VercelRequestData
{
    public string Owner { get; set; }               // "aarshiitaliya"
    public string RepoName { get; set; }            // "Test-Repository-static"
    public string Branch { get; set; }              // "main"
    public string? BuildCommand { get; set; }       // "npm run build" or blank
    public string? BuildDir { get; set; }           // "out" or "public"
    public string? InstallCommand { get; set; }     // "npm install" (optional)
    public string? Framework { get; set; }          // "nextjs", "vite", etc. (optional)
    public string? TeamId { get; set; }             // Optional team ID
    public object[]? EnvironmentVariables { get; set; } // Optional env vars
}