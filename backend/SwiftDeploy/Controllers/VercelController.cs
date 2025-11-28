//using Microsoft.AspNetCore.Mvc;
//using MongoDB.Bson.IO;
//using System;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Security.Cryptography;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;

//[ApiController]
//[Route("api/vercel")]
//public class VercelController : ControllerBase
//{
//    private const string BaseApiUrl = "https://api.vercel.com/v9";
//    private readonly ILogger<VercelController> _logger;

//    public VercelController(ILogger<VercelController> logger)
//    {
//        _logger = logger;
//    }

//    // Generate a unique project name using repo, branch, and timestamp
//    private string GenerateProjectName(string owner, string repo, string branch)
//    {
//        string raw = $"{owner}-{repo}-{branch}".ToLower();

//        // Vercel project names must be lowercase alphanumeric with hyphens
//        // Remove special characters and replace with hyphens
//        raw = System.Text.RegularExpressions.Regex.Replace(raw, "[^a-z0-9-]", "-");
//        raw = System.Text.RegularExpressions.Regex.Replace(raw, "-+", "-"); // Remove duplicate hyphens
//        raw = raw.Trim('-'); // Remove leading/trailing hyphens

//        // Add short hash for uniqueness
//        using var sha1 = SHA1.Create();
//        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(raw + DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
//        var hex = BitConverter.ToString(hash).Replace("-", "").ToLower();

//        var projectName = $"{raw}-{hex.Substring(0, 8)}";

//        // Vercel project names have a max length of 52 characters
//        if (projectName.Length > 52)
//        {
//            projectName = projectName.Substring(0, 52).TrimEnd('-');
//        }

//        return projectName;
//    }

//    [HttpPost("initiate")]
//    public async Task<IActionResult> Initiate(
//    [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
//    [FromBody] VercelRequestData requestData
//)
//    {
//        _logger.LogInformation("=== Vercel Deployment Initiation ===");

//        if (string.IsNullOrWhiteSpace(apiToken))
//            return BadRequest(new { error = "Missing Vercel API token in headers." });

//        if (requestData == null ||
//            string.IsNullOrWhiteSpace(requestData.Owner) ||
//            string.IsNullOrWhiteSpace(requestData.RepoName) ||
//            string.IsNullOrWhiteSpace(requestData.Branch))
//        {
//            return BadRequest(new { error = "Missing Owner, RepoName, or Branch in body." });
//        }

//        using var client = new HttpClient();
//        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

//        try
//        {
//            // ---------------------------------------------------------
//            // 1. Get Vercel user info (to build correct URLs)
//            // ---------------------------------------------------------
//            _logger.LogInformation("Fetching authenticated Vercel user info...");

//            var userResponse = await client.GetAsync("https://api.vercel.com/v2/user");
//            var userString = await userResponse.Content.ReadAsStringAsync();

//            if (!userResponse.IsSuccessStatusCode)
//                return StatusCode((int)userResponse.StatusCode, new { error = "Failed to authenticate", details = userString });

//            var userJson = JsonDocument.Parse(userString);
//            var userId = userJson.RootElement.TryGetProperty("user", out var user)
//                            ? user.GetProperty("id").GetString()
//                            : userJson.RootElement.GetProperty("id").GetString();

//            // ---------------------------------------------------------
//            // 2. Generate project name
//            // ---------------------------------------------------------
//            string projectName = GenerateProjectName(requestData.Owner, requestData.RepoName, requestData.Branch);
//            string githubRepo = $"{requestData.Owner}/{requestData.RepoName}";
//            _logger.LogInformation($"Project name generated: {projectName}");

//            // ---------------------------------------------------------
//            // 3. Create project payload
//            // ---------------------------------------------------------
//            var createPayload = new
//            {
//                name = projectName,
//                framework = requestData.Framework == "static" ? null : requestData.Framework,
//                buildCommand = string.IsNullOrWhiteSpace(requestData.BuildCommand) ? null : requestData.BuildCommand,
//                outputDirectory = string.IsNullOrWhiteSpace(requestData.BuildDir) ? "public" : requestData.BuildDir,
//                installCommand = string.IsNullOrWhiteSpace(requestData.InstallCommand) ? null : requestData.InstallCommand,

//                gitRepository = new
//                {
//                    type = "github",
//                    repo = githubRepo
//                },

//                environmentVariables = requestData.EnvironmentVariables ?? new object[] { }
//            };

//            var createUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
//                            ? $"{BaseApiUrl}/projects"
//                            : $"{BaseApiUrl}/projects?teamId={requestData.TeamId}";

//            var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
//            var createResponse = await client.PostAsync(createUrl, createContent);
//            var createBody = await createResponse.Content.ReadAsStringAsync();

//            if (!createResponse.IsSuccessStatusCode)
//                return StatusCode((int)createResponse.StatusCode, new { error = "Project creation failed", details = createBody });

//            var projectJson = JsonDocument.Parse(createBody);
//            string projectId = projectJson.RootElement.GetProperty("id").GetString();

//            _logger.LogInformation($"Project created successfully => ID: {projectId}");

//            // ---------------------------------------------------------
//            // 4. Link GitHub repo
//            // ---------------------------------------------------------
//            _logger.LogInformation("Linking GitHub repository...");

//            var linkUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
//                          ? $"{BaseApiUrl}/projects/{projectId}/link"
//                          : $"{BaseApiUrl}/projects/{projectId}/link?teamId={requestData.TeamId}";

//            var linkPayload = new
//            {
//                type = "github",
//                repo = githubRepo,
//                gitBranch = requestData.Branch
//            };

//            var linkContent = new StringContent(JsonSerializer.Serialize(linkPayload), Encoding.UTF8, "application/json");
//            var linkResponse = await client.PostAsync(linkUrl, linkContent);
//            var linkBody = await linkResponse.Content.ReadAsStringAsync();

//            if (!linkResponse.IsSuccessStatusCode)
//                _logger.LogWarning($"GitHub linking failed (may already be linked): {linkBody}");
//            else
//                _logger.LogInformation("GitHub repository linked successfully.");

//            // ---------------------------------------------------------
//            // 5. Trigger deployment manually
//            // ---------------------------------------------------------
//            _logger.LogInformation("Triggering deployment...");

//            var deployUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
//                            ? "https://api.vercel.com/v13/deployments"
//                            : $"https://api.vercel.com/v13/deployments?teamId={requestData.TeamId}";

//            var deployPayload = new
//            {
//                name = projectName,
//                project = projectId,
//                gitSource = new
//                {
//                    type = "github",
//                    repo = githubRepo,
//                    // NOTE: MUST MATCH GITHUB BRANCH
//                    @ref = requestData.Branch
//                },
//                target = "production"
//            };

//            var deployContent = new StringContent(JsonSerializer.Serialize(deployPayload), Encoding.UTF8, "application/json");
//            var deployResponse = await client.PostAsync(deployUrl, deployContent);
//            var deployBody = await deployResponse.Content.ReadAsStringAsync();

//            // deployment might still be queued but API call succeeds
//            bool deploymentOk = deployResponse.IsSuccessStatusCode;

//            // ---------------------------------------------------------
//            // Final API response to frontend
//            // ---------------------------------------------------------
//            return Ok(new
//            {
//                success = true,
//                projectId,
//                projectName,
//                githubRepo,
//                deploymentTriggered = deploymentOk,
//                projectInfo = JsonSerializer.Deserialize<object>(createBody),
//                deploymentInfo = JsonSerializer.Deserialize<object>(deployBody)
//            });
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Exception during Vercel Deployment");
//            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
//        }
//    }





//    [HttpGet("projects")]
//    public async Task<IActionResult> GetProjects(
//        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
//        [FromQuery] string? teamId = null
//    )
//    {
//        if (string.IsNullOrEmpty(apiToken))
//            return BadRequest(new { error = "Missing Vercel API token" });

//        using var client = new HttpClient();
//        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

//        var url = !string.IsNullOrEmpty(teamId)
//            ? $"{BaseApiUrl}/projects?teamId={teamId}"
//            : $"{BaseApiUrl}/projects";

//        var response = await client.GetAsync(url);
//        var result = await response.Content.ReadAsStringAsync();

//        if (!response.IsSuccessStatusCode)
//            return StatusCode((int)response.StatusCode, result);

//        return Ok(JsonSerializer.Deserialize<object>(result));
//    }

//    [HttpDelete("projects/{projectId}")]
//    public async Task<IActionResult> DeleteProject(
//        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
//        string projectId,
//        [FromQuery] string? teamId = null
//    )
//    {
//        if (string.IsNullOrEmpty(apiToken))
//            return BadRequest(new { error = "Missing Vercel API token" });

//        using var client = new HttpClient();
//        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

//        var url = !string.IsNullOrEmpty(teamId)
//            ? $"{BaseApiUrl}/projects/{projectId}?teamId={teamId}"
//            : $"{BaseApiUrl}/projects/{projectId}";

//        var response = await client.DeleteAsync(url);
//        var result = await response.Content.ReadAsStringAsync();

//        if (!response.IsSuccessStatusCode)
//            return StatusCode((int)response.StatusCode, result);

//        return Ok(new { success = true, message = "Project deleted successfully" });
//    }

//    [HttpGet("deployments/{projectId}")]
//    public async Task<IActionResult> GetDeployments(
//        [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
//        string projectId,
//        [FromQuery] string? teamId = null
//    )
//    {
//        if (string.IsNullOrEmpty(apiToken))
//            return BadRequest(new { error = "Missing Vercel API token" });

//        using var client = new HttpClient();
//        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

//        var url = !string.IsNullOrEmpty(teamId)
//            ? $"https://api.vercel.com/v6/deployments?projectId={projectId}&teamId={teamId}"
//            : $"https://api.vercel.com/v6/deployments?projectId={projectId}";

//        var response = await client.GetAsync(url);
//        var result = await response.Content.ReadAsStringAsync();

//        if (!response.IsSuccessStatusCode)
//            return StatusCode((int)response.StatusCode, result);

//        return Ok(JsonSerializer.Deserialize<object>(result));
//    }

//    // Helper method to detect framework from build command
//    private string DetectFramework(string? buildCommand)
//    {
//        if (string.IsNullOrEmpty(buildCommand))
//            return null;

//        buildCommand = buildCommand.ToLower();

//        if (buildCommand.Contains("next")) return "nextjs";
//        if (buildCommand.Contains("vite")) return "vite";
//        if (buildCommand.Contains("react")) return "create-react-app";
//        if (buildCommand.Contains("vue")) return "vue";
//        if (buildCommand.Contains("nuxt")) return "nuxtjs";
//        if (buildCommand.Contains("gatsby")) return "gatsby";
//        if (buildCommand.Contains("svelte")) return "svelte";
//        if (buildCommand.Contains("astro")) return "astro";

//        return null;
//    }
//}

//public class VercelRequestData
//{
//    public string Owner { get; set; }               // "aarshiitaliya"
//    public string RepoName { get; set; }            // "Test-Repository-static"
//    public string Branch { get; set; }

//    public long RepoId { get; set; }        // ⭐ NEW
//    public string CommitSha { get; set; } // "main"
//    public string? BuildCommand { get; set; }       // "npm run build" or blank
//    public string? BuildDir { get; set; }           // "out" or "public"
//    public string? InstallCommand { get; set; }     // "npm install" (optional)
//    public string? Framework { get; set; }          // "nextjs", "vite", etc. (optional)
//    public string? TeamId { get; set; }             // Optional team ID
//    public object[]? EnvironmentVariables { get; set; } // Optional env vars
//}




using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SwiftDeploy.Controllers
{
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
            raw = System.Text.RegularExpressions.Regex.Replace(raw, "[^a-z0-9-]", "-");
            raw = System.Text.RegularExpressions.Regex.Replace(raw, "-+", "-");
            raw = raw.Trim('-');

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
    [FromBody] VercelRequestData requestData)
        {
            _logger.LogInformation("=== Vercel Deployment Initiation ===");

            if (string.IsNullOrWhiteSpace(apiToken))
                return BadRequest(new { error = "Missing Vercel API token in headers." });

            if (requestData == null ||
                string.IsNullOrWhiteSpace(requestData.Owner) ||
                string.IsNullOrWhiteSpace(requestData.RepoName) ||
                string.IsNullOrWhiteSpace(requestData.Branch))
            {
                return BadRequest(new { error = "Missing Owner, RepoName, or Branch in body." });
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            try
            {
                // ---------------------------------------------------------
                // 1. Get Vercel user info
                // ---------------------------------------------------------
                _logger.LogInformation("Fetching authenticated Vercel user info...");

                var userResponse = await client.GetAsync("https://api.vercel.com/v2/user");
                var userString = await userResponse.Content.ReadAsStringAsync();

                if (!userResponse.IsSuccessStatusCode)
                    return StatusCode((int)userResponse.StatusCode, new { error = "Failed to authenticate", details = userString });

                var userJson = JsonDocument.Parse(userString);
                var userId = userJson.RootElement.TryGetProperty("user", out var user)
                                ? user.GetProperty("id").GetString()
                                : userJson.RootElement.GetProperty("id").GetString();

                // ---------------------------------------------------------
                // 2. Generate project name
                // ---------------------------------------------------------
                string projectName = GenerateProjectName(requestData.Owner, requestData.RepoName, requestData.Branch);
                string githubRepo = $"{requestData.Owner}/{requestData.RepoName}";
                _logger.LogInformation($"Project name generated: {projectName}");

                // ---------------------------------------------------------
                // 3. Check existing projects to avoid repo_links_exceeded_limit
                // ---------------------------------------------------------
                _logger.LogInformation("Checking existing projects for repository connection limit...");

                var projectsUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
                                ? $"{BaseApiUrl}/projects"
                                : $"{BaseApiUrl}/projects?teamId={requestData.TeamId}";

                var projectsResponse = await client.GetAsync(projectsUrl);
                var projectsBody = await projectsResponse.Content.ReadAsStringAsync();

                if (projectsResponse.IsSuccessStatusCode)
                {
                    var projectsJson = JsonDocument.Parse(projectsBody);
                    if (projectsJson.RootElement.TryGetProperty("projects", out var projects))
                    {
                        int connectedCount = 0;
                        var connectedProjectNames = new List<string>();

                        foreach (var project in projects.EnumerateArray())
                        {
                            if (project.TryGetProperty("link", out var link) &&
                                link.TryGetProperty("repo", out var repo) &&
                                repo.GetString() == githubRepo)
                            {
                                connectedCount++;
                                if (project.TryGetProperty("name", out var name))
                                {
                                    connectedProjectNames.Add(name.GetString());
                                }
                            }
                        }

                        _logger.LogInformation($"Repository {githubRepo} is connected to {connectedCount}/10 projects");

                        if (connectedCount >= 10)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                error = "Repository connection limit exceeded",
                                message = $"The repository '{githubRepo}' is already connected to {connectedCount} Vercel projects. Vercel allows a maximum of 10 connections per repository.",
                                suggestion = "Please delete some existing projects using the DELETE /api/vercel/projects/{{projectId}} endpoint, or use a different repository.",
                                connectedProjects = connectedProjectNames,
                                connectedCount = connectedCount,
                                limit = 10,
                                learnMore = "https://vercel.link/repository-connection-limit"
                            });
                        }
                    }
                }

                // ---------------------------------------------------------
                // 4. Create project payload
                // ---------------------------------------------------------
                _logger.LogInformation("Creating Vercel project...");

                var createPayload = new
                {
                    name = projectName,
                    framework = requestData.Framework == "static" ? null : requestData.Framework,
                    buildCommand = string.IsNullOrWhiteSpace(requestData.BuildCommand) ? null : requestData.BuildCommand,
                    outputDirectory = string.IsNullOrWhiteSpace(requestData.BuildDir) ? "public" : requestData.BuildDir,
                    installCommand = string.IsNullOrWhiteSpace(requestData.InstallCommand) ? null : requestData.InstallCommand,

                    gitRepository = new
                    {
                        type = "github",
                        repo = githubRepo
                    },

                    environmentVariables = requestData.EnvironmentVariables ?? new object[] { }
                };

                var createUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
                                ? $"{BaseApiUrl}/projects"
                                : $"{BaseApiUrl}/projects?teamId={requestData.TeamId}";

                var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
                var createResponse = await client.PostAsync(createUrl, createContent);
                var createBody = await createResponse.Content.ReadAsStringAsync();

                if (!createResponse.IsSuccessStatusCode)
                {
                    // Parse error details
                    try
                    {
                        var errorJson = JsonDocument.Parse(createBody);
                        var errorCode = errorJson.RootElement.GetProperty("error").GetProperty("code").GetString();
                        var errorMessage = errorJson.RootElement.GetProperty("error").GetProperty("message").GetString();

                        return StatusCode((int)createResponse.StatusCode, new
                        {
                            success = false,
                            error = "Project creation failed",
                            code = errorCode,
                            message = errorMessage,
                            details = createBody
                        });
                    }
                    catch
                    {
                        return StatusCode((int)createResponse.StatusCode, new
                        {
                            success = false,
                            error = "Project creation failed",
                            details = createBody
                        });
                    }
                }

                var projectJson = JsonDocument.Parse(createBody);
                string projectId = projectJson.RootElement.GetProperty("id").GetString();

                _logger.LogInformation($"Project created successfully => ID: {projectId}");

                // ---------------------------------------------------------
                // 5. Link GitHub repo
                // ---------------------------------------------------------
                _logger.LogInformation("Linking GitHub repository...");

                var linkUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
                              ? $"{BaseApiUrl}/projects/{projectId}/link"
                              : $"{BaseApiUrl}/projects/{projectId}/link?teamId={requestData.TeamId}";

                var linkPayload = new
                {
                    type = "github",
                    repo = githubRepo,
                    gitBranch = requestData.Branch
                };

                var linkContent = new StringContent(JsonSerializer.Serialize(linkPayload), Encoding.UTF8, "application/json");
                var linkResponse = await client.PostAsync(linkUrl, linkContent);
                var linkBody = await linkResponse.Content.ReadAsStringAsync();

                if (!linkResponse.IsSuccessStatusCode)
                    _logger.LogWarning($"GitHub linking failed (may already be linked): {linkBody}");
                else
                    _logger.LogInformation("GitHub repository linked successfully.");

                // ---------------------------------------------------------
                // 6. Trigger deployment manually
                // ---------------------------------------------------------
                _logger.LogInformation("Triggering deployment...");

                var deployUrl = string.IsNullOrWhiteSpace(requestData.TeamId)
                                ? "https://api.vercel.com/v13/deployments"
                                : $"https://api.vercel.com/v13/deployments?teamId={requestData.TeamId}";

                var deployPayload = new
                {
                    name = projectName,
                    project = projectId,
                    gitSource = new
                    {
                        type = "github",
                        repo = githubRepo,
                        repoId = requestData.RepoId,  // ← FIXED: Added repoId
                        @ref = requestData.Branch
                    },
                    target = "production"
                };

                var deployContent = new StringContent(JsonSerializer.Serialize(deployPayload), Encoding.UTF8, "application/json");
                var deployResponse = await client.PostAsync(deployUrl, deployContent);
                var deployBody = await deployResponse.Content.ReadAsStringAsync();

                bool deploymentOk = deployResponse.IsSuccessStatusCode;

                // Get deployment URL if available
                string deploymentUrl = null;
                string deploymentId = null;
                if (deploymentOk)
                {
                    try
                    {
                        var deployJson = JsonDocument.Parse(deployBody);
                        if (deployJson.RootElement.TryGetProperty("url", out var urlProp))
                        {
                            deploymentUrl = $"https://{urlProp.GetString()}";
                        }
                        if (deployJson.RootElement.TryGetProperty("id", out var idProp))
                        {
                            deploymentId = idProp.GetString();
                        }
                    }
                    catch { }
                }

                _logger.LogInformation($"Deployment triggered: {deploymentOk}, URL: {deploymentUrl ?? "pending"}");

                // ---------------------------------------------------------
                // Final API response to frontend
                // ---------------------------------------------------------
                return Ok(new
                {
                    success = true,
                    projectId,
                    projectName,
                    githubRepo,
                    deploymentId = deploymentId,
                    deploymentUrl = deploymentUrl ?? "pending",
                    deploymentTriggered = deploymentOk,
                    projectInfo = JsonSerializer.Deserialize<object>(createBody),
                    deploymentInfo = deploymentOk ? JsonSerializer.Deserialize<object>(deployBody) : new { error = deployBody }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during Vercel Deployment");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    details = ex.ToString()
                });
            }
        }





        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects(
            [FromHeader(Name = "Vercel-Api-Token")] string apiToken,
            [FromQuery] string? teamId = null)
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
            [FromQuery] string? teamId = null)
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
            [FromQuery] string? teamId = null)
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
        public string Owner { get; set; }
        public string RepoName { get; set; }
        public string Branch { get; set; }
        public long RepoId { get; set; }
        public string CommitSha { get; set; }
        public string? BuildCommand { get; set; }
        public string? BuildDir { get; set; }
        public string? InstallCommand { get; set; }
        public string? Framework { get; set; }
        public string? TeamId { get; set; }
        public object[]? EnvironmentVariables { get; set; }
    }
}