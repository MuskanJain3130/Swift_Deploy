// Controllers/UnifiedDeploymentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Services;
using SwiftDeploy.Services.Interfaces;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using DeploymentStatus = SwiftDeploy.Models.DeploymentStatus;
namespace SwiftDeploy.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UnifiedDeploymentController : ControllerBase
    {
        private readonly IUnifiedDeploymentService _deploymentService;
        private readonly ITemplateEngine _templateEngine;
        private readonly ILogger<UnifiedDeploymentController> _logger;
        private static readonly ConcurrentDictionary<string, ProjectInfo> _projects = new();
        private readonly IConfiguration _configuration;
        IHttpContextAccessor _httpContextAccessor; // ADD THIS

        // Remove this line:
        private readonly TokenService _tokenService;

        public UnifiedDeploymentController(
            IUnifiedDeploymentService deploymentService,
            ITemplateEngine templateEngine,
            IConfiguration configuration,
            TokenService tokenService,
            ILogger<UnifiedDeploymentController> logger,
                IHttpContextAccessor httpContextAccessor // ADD THIS
)
        {
            _deploymentService = deploymentService;
            _templateEngine = templateEngine;
            _configuration = configuration;
            _tokenService = tokenService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost("deploy-without-github")]
        [Authorize]
        public async Task<IActionResult> DeployWithoutGitHub([FromForm] UploadProjectRequest request)
        {
            var projectId = Guid.NewGuid().ToString();

            try
            {
                // Validate request
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Invalid token");

                var platformToken = await _tokenService.GetPlatformTokenAsync(userId, request.Platform, HttpContext);


                var supportedPlatforms = new[] { "vercel", "cloudflare", "netlify" };
                if (!supportedPlatforms.Contains(request.Platform.ToLower()))
                    return BadRequest($"Unsupported platform: {request.Platform}");

                // Initialize project tracking
                var projectInfo = new ProjectInfo
                {
                    ProjectId = projectId,
                    ProjectName = request.ProjectName,
                    Description = request.Description,
                    Platform = request.Platform.ToLower(),
                    CreatedAt = DateTime.UtcNow,
                    Status = DeploymentStatus.Uploading,
                    Config = request.Config
                };
                _projects[projectId] = projectInfo;

                _logger.LogInformation($"Starting deployment for project {request.ProjectName} on {request.Platform}");

                // Step 1: Upload and extract project
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Processing, "Extracting project files...");
                var localProjectPath = await _deploymentService.UploadAndExtractProjectAsync(request.ProjectZip, request.ProjectName);

                // Step 2: Create GitHub repo on SwiftDeploy account
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.CreatingRepo, "Creating GitHub repository...");
                var repoName = await _deploymentService.CreateSwiftDeployRepoAsync(request.ProjectName, request.Description);
                projectInfo.GitHubRepoName = repoName;
                projectInfo.GitHubRepoUrl = $"https://github.com/swiftdeploy-repos/{repoName}";

                // Step 3: Push code to repo
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.PushingCode, "Pushing code to repository...");
                await _deploymentService.PushCodeToSwiftDeployRepoAsync(repoName, localProjectPath);

                // Step 4: Generate and push config
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.GeneratingConfig, "Generating deployment configuration...");
                await _deploymentService.PushConfigToRepoAsync(repoName, request.Platform, request.Config);

                // Step 5: Deploy to platform
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Deploying, $"Deploying to {request.Platform}...");

                DeploymentResponse deploymentResult = request.Platform.ToLower() switch
                {
                    "cloudflare" => await _deploymentService.DeployToCloudflareAsync(repoName, "main", request.Config, userId, platformToken),
                    "netlify" => await _deploymentService.DeployToNetlifyAsync(repoName, "main", request.Config, userId, platformToken),
                    "vercel" => await _deploymentService.DeployToVercelAsync(repoName, "main", request.Config, userId, platformToken),
                    _ => throw new ArgumentException($"Unsupported platform: {request.Platform}")
                };

                if (deploymentResult.Success)
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Completed, "Deployment completed successfully!");
                    projectInfo.DeploymentUrl = deploymentResult.DeploymentUrl;
                    projectInfo.Status = DeploymentStatus.Completed;
                }
                else
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, deploymentResult.Message);
                    projectInfo.Status = DeploymentStatus.Failed;
                }

                return Ok(new DeploymentResponse
                {
                    Success = deploymentResult.Success,
                    Message = deploymentResult.Message,
                    ProjectId = projectId,
                    GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                    DeploymentUrl = deploymentResult.DeploymentUrl,
                    ConfigFileUrl = deploymentResult.ConfigFileUrl,
                    Status = projectInfo.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Deployment failed for project {request.ProjectName}");
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, ex.Message);

                return StatusCode(500, new DeploymentResponse
                {
                    Success = false,
                    Message = $"Deployment failed: {ex.Message}",
                    ProjectId = projectId,
                    Status = DeploymentStatus.Failed
                });
            }
        }
        [HttpGet("status/{projectId}")]
        public async Task<IActionResult> GetDeploymentStatus(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return NotFound("Project not found");

            return Ok(new DeploymentStatusResponse
            {
                ProjectId = projectId,
                Status = project.Status,
                Message = GetStatusMessage(project.Status),
                Progress = GetProgressPercentage(project.Status),
                CurrentStep = GetCurrentStep(project.Status),
                DeploymentUrl = project.DeploymentUrl,
                GitHubRepoUrl = project.GitHubRepoUrl
            });
        }

        // Helper method for status messages
        private string GetStatusMessage(DeploymentStatus status)
        {
            return status switch
            {
                DeploymentStatus.Uploading => "Uploading project files...",
                DeploymentStatus.Processing => "Processing and extracting files...",
                DeploymentStatus.CreatingRepo => "Creating GitHub repository...",
                DeploymentStatus.PushingCode => "Pushing code to repository...",
                DeploymentStatus.GeneratingConfig => "Generating deployment configuration...",
                DeploymentStatus.Deploying => "Deploying to platform...",
                DeploymentStatus.Completed => "Deployment completed successfully!",
                DeploymentStatus.Failed => "Deployment failed.",
                _ => "Unknown status"
            };
        }

        // Helper method for progress percentage
        private int GetProgressPercentage(DeploymentStatus status)
        {
            return status switch
            {
                DeploymentStatus.Uploading => 10,
                DeploymentStatus.Processing => 25,
                DeploymentStatus.CreatingRepo => 40,
                DeploymentStatus.PushingCode => 60,
                DeploymentStatus.GeneratingConfig => 75,
                DeploymentStatus.Deploying => 90,
                DeploymentStatus.Completed => 100,
                DeploymentStatus.Failed => 0,
                _ => 0
            };
        }

        // Helper method for current step
        private string GetCurrentStep(DeploymentStatus status)
        {
            return status switch
            {
                DeploymentStatus.Uploading => "Step 1/6: Uploading Files",
                DeploymentStatus.Processing => "Step 2/6: Processing Files",
                DeploymentStatus.CreatingRepo => "Step 3/6: Creating Repository",
                DeploymentStatus.PushingCode => "Step 4/6: Pushing Code",
                DeploymentStatus.GeneratingConfig => "Step 5/6: Generating Config",
                DeploymentStatus.Deploying => "Step 6/6: Deploying",
                DeploymentStatus.Completed => "Completed",
                DeploymentStatus.Failed => "Failed",
                _ => "Unknown"
            };
        }
        [HttpPost("deploy-with-github")]
        public async Task<IActionResult> DeployWithGitHub([FromBody] GitHubDeployRequest request)
        {
            var projectId = Guid.NewGuid().ToString();

            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var supportedPlatforms = new[] { "vercel", "cloudflare", "netlify" };
                if (!supportedPlatforms.Contains(request.Platform.ToLower()))
                    return BadRequest($"Unsupported platform: {request.Platform}");

                // ⭐ Get GitHub token from service (checks header then database)
                var githubToken = await ((UnifiedDeploymentService)_deploymentService).GetGitHubTokenForUserAsync(request.UserId);
                if (string.IsNullOrEmpty(githubToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "GitHub token not found. Please connect your GitHub account first.",
                        platform = "github",
                        userId = request.UserId
                    });
                }

                // ⭐ ADD THIS: Get platform token (Cloudflare/Netlify/Vercel)
                var platformToken = await _tokenService.GetPlatformTokenAsync(request.UserId, request.Platform, HttpContext);
                if (string.IsNullOrEmpty(platformToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"No {request.Platform} token found. Please connect your {request.Platform} account.",
                        platform = request.Platform,
                        userId = request.UserId
                    });
                }

                // Initialize project tracking
                var projectInfo = new ProjectInfo
                {
                    ProjectId = projectId,
                    ProjectName = request.ProjectName,
                    Description = request.Description,
                    Platform = request.Platform.ToLower(),
                    CreatedAt = DateTime.UtcNow,
                    Status = DeploymentStatus.GeneratingConfig,
                    Config = request.Config,
                    GitHubRepoName = request.GitHubRepo,
                    GitHubRepoUrl = $"https://github.com/{request.GitHubRepo}"
                };
                _projects[projectId] = projectInfo;

                _logger.LogInformation($"Starting GitHub deployment for {request.GitHubRepo} on {request.Platform}");

                // Step 1: Generate and save config
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.GeneratingConfig, "Generating and saving configuration...");

                var configContent = await _templateEngine.GenerateConfigAsync(request.Platform, request.Config);
                var fileName = _templateEngine.GetConfigFileName(request.Platform);

                // Push config to user's GitHub repo - ⭐ Use githubToken variable
                var gitHubService = HttpContext.RequestServices.GetRequiredService<IGitHubService>();
                var configResult = await gitHubService.GenerateAndSaveConfigAsync(
                    request.Platform,
                    request.GitHubRepo,
                    githubToken,  // ⭐ Changed from request.GitHubToken
                    request.Branch ?? "main",
                    request.Config
                );

                if (!configResult.Success)
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed,
                        $"Failed to save config: {configResult.Message}");

                    return BadRequest(new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to save config to GitHub: {configResult.Message}",
                        ProjectId = projectId,
                        GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        Status = DeploymentStatus.Failed
                    });
                }

                // Step 2: Deploy to platform - ⭐ Now platformToken is defined
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Deploying, $"Deploying to {request.Platform}...");

                DeploymentResponse deploymentResult = request.Platform.ToLower() switch
                {
                    "cloudflare" => await DeployToCloudflareWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, platformToken, githubToken),
                    "netlify" => await DeployToNetlifyWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, platformToken, githubToken),
                    "vercel" => await DeployToVercelWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, platformToken, githubToken),
                    _ => throw new ArgumentException($"Unsupported platform: {request.Platform}")
                };

                if (deploymentResult.Success)
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Completed, "Deployment completed successfully!");
                    projectInfo.DeploymentUrl = deploymentResult.DeploymentUrl;
                    projectInfo.Status = DeploymentStatus.Completed;
                }
                else
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, deploymentResult.Message);
                    projectInfo.Status = DeploymentStatus.Failed;
                }

                return Ok(new DeploymentResponse
                {
                    Success = deploymentResult.Success,
                    Message = deploymentResult.Message,
                    ProjectId = projectId,
                    GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                    DeploymentUrl = deploymentResult.DeploymentUrl,
                    ConfigFileUrl = configResult.FileUrl,
                    Status = projectInfo.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GitHub deployment failed for {request.GitHubRepo}");
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, ex.Message);

                return StatusCode(500, new DeploymentResponse
                {
                    Success = false,
                    Message = $"Deployment failed: {ex.Message}",
                    ProjectId = projectId,
                    Status = DeploymentStatus.Failed
                });
            }
        }


        [HttpGet("projects")]
        public async Task<IActionResult> GetUserProjects([FromQuery] string userId = null)
        {
            try
            {
                // If no userId provided, return all projects (for admin/testing)
                // In production, you'd get userId from authentication context
                var userProjects = _projects.Values
                    .Where(p => userId == null || p.ProjectId.Contains(userId)) // Simple filter for demo
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        p.ProjectId,
                        p.ProjectName,
                        p.Description,
                        p.Platform,
                        p.Status,
                        p.CreatedAt,
                        p.GitHubRepoUrl,
                        p.DeploymentUrl,
                        StatusMessage = GetStatusMessage(p.Status),
                        Progress = GetProgressPercentage(p.Status)
                    })
                    .ToList();

                return Ok(new
                {
                    projects = userProjects,
                    total = userProjects.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user projects");
                return StatusCode(500, "Error retrieving projects");
            }
        }

        [HttpGet("projects/{projectId}")]
        public async Task<IActionResult> GetProjectDetails(string projectId)
        {
            try
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return NotFound("Project not found");

                return Ok(new
                {
                    project.ProjectId,
                    project.ProjectName,
                    project.Description,
                    project.Platform,
                    project.Status,
                    project.CreatedAt,
                    project.GitHubRepoName,
                    project.GitHubRepoUrl,
                    project.DeploymentUrl,
                    project.Config,
                    StatusMessage = GetStatusMessage(project.Status),
                    Progress = GetProgressPercentage(project.Status),
                    CurrentStep = GetCurrentStep(project.Status)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving project details for {projectId}");
                return StatusCode(500, "Error retrieving project details");
            }
        }// Helper function for Cloudflare deployment
        private async Task<DeploymentResponse> DeployToCloudflareWithConfig(string repoPath, string branch, CommonConfig config)
        {
            try
            {
                var swiftDeployToken = _configuration["SwiftDeploy:CloudflareToken"];
                if (string.IsNullOrEmpty(swiftDeployToken))
                    throw new Exception("SwiftDeploy Cloudflare token not configured");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", swiftDeployToken);

                // Get account ID
                var userResponse = await client.GetAsync("https://api.cloudflare.com/client/v4/accounts");
                var userString = await userResponse.Content.ReadAsStringAsync();
                if (!userResponse.IsSuccessStatusCode)
                    throw new Exception($"Failed to get Cloudflare account: {userString}");

                var userJson = JsonDocument.Parse(userString);
                string accountId = userJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

                // Generate project name
                string projectName = GenerateCloudflareProjectName(repoPath, branch);

                // Create project
                var createProjectUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects";
                var createPayload = new
                {
                    name = projectName,
                    production_branch = branch,
                    source = new
                    {
                        type = "github",
                        config = new
                        {
                            git_provider = "github",
                            owner = "swiftdeploy-repos", // Your organization
                            repo_name = repoPath.Split('/').Last(),
                            branch = branch
                        }
                    },
                    build_config = new
                    {
                        build_command = config.BuildCommand,
                        destination_dir = config.OutputDirectory,
                        root_dir = ""
                    }
                };

                var createContent = new StringContent(JsonSerializer.Serialize(createPayload), System.Text.Encoding.UTF8, "application/json");
                var createResponse = await client.PostAsync(createProjectUrl, createContent);
                var createResult = await createResponse.Content.ReadAsStringAsync();

                if (!createResponse.IsSuccessStatusCode)
                    throw new Exception($"Cloudflare project creation failed: {createResult}");

                var createData = JsonDocument.Parse(createResult);
                var deploymentUrl = createData.RootElement.GetProperty("result").GetProperty("subdomain").GetString() + ".pages.dev";

                // Trigger deployment
                var deployUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}/deployments";
                var deployResponse = await client.PostAsync(deployUrl, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                return new DeploymentResponse
                {
                    Success = deployResponse.IsSuccessStatusCode,
                    Message = deployResponse.IsSuccessStatusCode ? "Cloudflare deployment started successfully" : "Cloudflare deployment failed",
                    DeploymentUrl = $"https://{deploymentUrl}"
                };
            }
            catch (Exception ex)
            {
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Cloudflare deployment error: {ex.Message}"
                };
            }
        }

        // Helper function for Netlify deployment
        private async Task<DeploymentResponse> DeployToNetlifyWithConfig(string repoPath, string branch, CommonConfig config)
        {
            try
            {
                var swiftDeployToken = _configuration["SwiftDeploy:NetlifyToken"];
                if (string.IsNullOrEmpty(swiftDeployToken))
                    throw new Exception("SwiftDeploy Netlify token not configured");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", swiftDeployToken);

                var sitePayload = new
                {
                    name = $"swiftdeploy-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    repo = new
                    {
                        provider = "github",
                        repo = $"swiftdeploy-repos/{repoPath.Split('/').Last()}",
                        branch = branch
                    },
                    build_settings = new
                    {
                        cmd = config.BuildCommand,
                        dir = config.OutputDirectory
                    }
                };

                var createSiteResp = await client.PostAsJsonAsync("https://api.netlify.com/api/v1/sites", sitePayload);
                var siteResponseBody = await createSiteResp.Content.ReadAsStringAsync();

                if (!createSiteResp.IsSuccessStatusCode)
                    throw new Exception($"Netlify site creation failed: {siteResponseBody}");

                var siteData = JsonDocument.Parse(siteResponseBody);
                var siteId = siteData.RootElement.GetProperty("id").GetString();
                var siteUrl = siteData.RootElement.GetProperty("url").GetString();

                // Trigger build
                var buildResp = await client.PostAsync($"https://api.netlify.com/api/v1/sites/{siteId}/builds", null);

                return new DeploymentResponse
                {
                    Success = buildResp.IsSuccessStatusCode,
                    Message = buildResp.IsSuccessStatusCode ? "Netlify deployment started successfully" : "Netlify deployment failed",
                    DeploymentUrl = siteUrl
                };
            }
            catch (Exception ex)
            {
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Netlify deployment error: {ex.Message}"
                };
            }
        }

        // Helper function for Vercel deployment
        private async Task<DeploymentResponse> DeployToVercelWithConfig(string repoPath, string branch, CommonConfig config)
        {
            try
            {
                var swiftDeployToken = _configuration["SwiftDeploy:VercelToken"];
                if (string.IsNullOrEmpty(swiftDeployToken))
                    throw new Exception("SwiftDeploy Vercel token not configured");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", swiftDeployToken);

                var payload = new
                {
                    name = $"swiftdeploy-{Guid.NewGuid().ToString()[..8]}",
                    gitRepository = new
                    {
                        type = "github",
                        repo = $"swiftdeploy-repos/{repoPath.Split('/').Last()}",
                        ref_ = branch
                    },
                    buildCommand = config.BuildCommand,
                    outputDirectory = config.OutputDirectory,
                    installCommand = config.InstallCommand
                };

                var deployResponse = await client.PostAsJsonAsync("https://api.vercel.com/v13/deployments", payload);
                var body = await deployResponse.Content.ReadAsStringAsync();

                if (!deployResponse.IsSuccessStatusCode)
                    throw new Exception($"Vercel deployment failed: {body}");

                var data = JsonDocument.Parse(body);
                var deploymentUrl = data.RootElement.GetProperty("url").GetString();

                return new DeploymentResponse
                {
                    Success = true,
                    Message = "Vercel deployment started successfully",
                    DeploymentUrl = $"https://{deploymentUrl}"
                };
            }
            catch (Exception ex)
            {
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Vercel deployment error: {ex.Message}"
                };
            }
        }// Helper function to generate Cloudflare project name
        private string GenerateCloudflareProjectName(string repo, string branch)
        {
            string raw = $"{repo}-{branch}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return $"swiftdeploy-{hex.Substring(0, 10)}";
        }

        [HttpDelete("projects/{projectId}")]
        public async Task<IActionResult> DeleteProject(string projectId)
        {
            try
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return NotFound("Project not found");

                // Remove from tracking
                _projects.TryRemove(projectId, out _);

                // Optional: Clean up files (implement based on your storage strategy)
                // await CleanupProjectFiles(project);

                _logger.LogInformation($"Project {projectId} deleted successfully");

                return Ok(new { message = "Project deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting project {projectId}");
                return StatusCode(500, "Error deleting project");
            }
        }

        [HttpPost("regenerate-config/{projectId}")]
        public async Task<IActionResult> RegenerateConfig(string projectId, [FromBody] CommonConfig newConfig)
        {
            try
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return NotFound("Project not found");

                // Update project config
                project.Config = newConfig;
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.GeneratingConfig, "Regenerating configuration...");

                // Generate new config
                var configContent = await _templateEngine.GenerateConfigAsync(project.Platform, newConfig);
                var fileName = _templateEngine.GetConfigFileName(project.Platform);

                // Push updated config to repo
                var gitHubService = HttpContext.RequestServices.GetRequiredService<IGitHubService>();
                var configResult = await gitHubService.SaveFileToRepoAsync(
                    project.GitHubRepoName,
                    fileName,
                    configContent,
                    "Update configuration via SwiftDeploy",
                    "main",
                    _configuration["SwiftDeploy:GitHubToken"]
                );

                if (configResult.Success)
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Completed, "Configuration updated successfully");
                    return Ok(new
                    {
                        success = true,
                        message = "Configuration regenerated successfully",
                        configFileUrl = configResult.FileUrl
                    });
                }
                else
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, configResult.Message);
                    return BadRequest(new
                    {
                        success = false,
                        message = configResult.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error regenerating config for project {projectId}");
                return StatusCode(500, "Error regenerating configuration");
            }
        }// Add these methods at the end of your UnifiedDeploymentController class

        // Deploy to Netlify using USER'S GitHub repo
        // In UnifiedDeploymentController, replace DeployToNetlifyWithUserRepo method:

        private async Task<DeploymentResponse> DeployToNetlifyWithUserRepo(string repoPath, string branch, CommonConfig config, string netlifyToken, string githubToken)
        {
            try
            {
                _logger.LogInformation($"Calling existing Netlify deployment endpoint for {repoPath}");

                // Get GitHub token from cookies
                var GithubToken = githubToken ?? _httpContextAccessor.HttpContext.Request.Cookies["GitHubAccessToken"];
                if (string.IsNullOrEmpty(githubToken))
                {
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = "GitHub token not found. Please authenticate with GitHub first."
                    };
                }

                using var client = new HttpClient();

                // Set cookies for the internal request
                var cookieContainer = new System.Net.CookieContainer();
                var handler = new HttpClientHandler { CookieContainer = cookieContainer };
                using var clientWithCookies = new HttpClient(handler);

                // Add cookies
                var baseUri = new Uri($"{Request.Scheme}://{Request.Host}");
                cookieContainer.Add(baseUri, new System.Net.Cookie("NetlifyAccessToken", netlifyToken));
                cookieContainer.Add(baseUri, new System.Net.Cookie("GitHubAccessToken", GithubToken));

                // Add authorization header if user is authenticated
                var authHeader = _httpContextAccessor.HttpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    clientWithCookies.DefaultRequestHeaders.Add("Authorization", authHeader);
                }

                // Call the existing endpoint
                var deployPayload = new
                {
                    Repo = repoPath,
                    Branch = branch
                };

                var response = await clientWithCookies.PostAsJsonAsync(
                    $"{Request.Scheme}://{Request.Host}/api/netlify/deploy",
                    deployPayload
                );

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    var siteUrl = result.GetProperty("site_url").GetString();

                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Netlify deployment started successfully",
                        DeploymentUrl = siteUrl
                    };
                }
                else
                {
                    _logger.LogError($"Netlify deployment failed: {responseBody}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Netlify deployment failed: {responseBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Netlify deployment endpoint");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Netlify deployment error: {ex.Message}"
                };
            }
        }
        private async Task<DeploymentResponse> DeployToCloudflareWithUserRepo(string repoPath, string branch, CommonConfig config, string cloudflareToken,string githubToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cloudflareToken);

                // Get account ID
                var userResponse = await client.GetAsync("https://api.cloudflare.com/client/v4/accounts");
                var userString = await userResponse.Content.ReadAsStringAsync();
                if (!userResponse.IsSuccessStatusCode)
                    throw new Exception($"Failed to get Cloudflare account: {userString}");

                var userJson = JsonDocument.Parse(userString);
                string accountId = userJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

                // Generate project name
                string projectName = GenerateCloudflareProjectName(repoPath, branch);

                // Split repo path (e.g., "tamannashah18/Test-Repository-static")
                var repoParts = repoPath.Split('/');
                var owner = repoParts[0];
                var repoName = repoParts[1];

                // Create project
                var createProjectUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects";
                var createPayload = new
                {
                    name = projectName,
                    production_branch = branch,
                    source = new
                    {
                        type = "github",
                        config = new
                        {
                            owner = owner,
                            repo_name = repoName,
                            production_branch = branch
                        }
                    },
                    build_config = new
                    {
                        build_command = config.BuildCommand ?? "",
                        destination_dir = config.OutputDirectory ?? ".",
                        root_dir = ""
                    }
                };

                var createContent = new StringContent(JsonSerializer.Serialize(createPayload), System.Text.Encoding.UTF8, "application/json");
                var createResponse = await client.PostAsync(createProjectUrl, createContent);
                var createResult = await createResponse.Content.ReadAsStringAsync();

                if (!createResponse.IsSuccessStatusCode)
                    throw new Exception($"Cloudflare project creation failed: {createResult}");

                var createData = JsonDocument.Parse(createResult);
                var deploymentUrl = createData.RootElement.GetProperty("result").GetProperty("subdomain").GetString() ;

                // Trigger deployment
                var deployUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}/deployments";
                var deployResponse = await client.PostAsync(deployUrl, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                return new DeploymentResponse
                {
                    Success = deployResponse.IsSuccessStatusCode,
                    Message = deployResponse.IsSuccessStatusCode ? "Cloudflare deployment started successfully" : "Cloudflare deployment failed",
                    DeploymentUrl = $"https://{deploymentUrl}"
                };
            }
            catch (Exception ex)
            {
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Cloudflare deployment error: {ex.Message}"
                };
            }
        }

        private async Task<DeploymentResponse> DeployToVercelWithUserRepo(string repoPath, string branch, CommonConfig config, string vercelToken, string githubToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", vercelToken);

                // Split repo path (e.g., "tamannashah18/Test-Repository-static")
                var repoParts = repoPath.Split('/');
                var owner = repoParts[0];
                var repoName = repoParts[1];

                var payload = new
                {
                    name = $"swiftdeploy-{Guid.NewGuid().ToString()[..8]}",
                    gitSource = new
                    {
                        type = "github",
                        repo = repoPath,
                        ref_ = branch
                    },
                    buildCommand = config.BuildCommand ?? "",
                    outputDirectory = config.OutputDirectory ?? ".",
                    installCommand = config.InstallCommand ?? "npm install"
                };

                var deployResponse = await client.PostAsJsonAsync("https://api.vercel.com/v13/deployments", payload);
                var body = await deployResponse.Content.ReadAsStringAsync();

                if (!deployResponse.IsSuccessStatusCode)
                    throw new Exception($"Vercel deployment failed: {body}");

                var data = JsonDocument.Parse(body);
                var deploymentUrl = data.RootElement.GetProperty("url").GetString();

                return new DeploymentResponse
                {
                    Success = true,
                    Message = "Vercel deployment started successfully",
                    DeploymentUrl = $"https://{deploymentUrl}"
                };
            }
            catch (Exception ex)
            {
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Vercel deployment error: {ex.Message}"
                };
            }
        }

    } // ← This closes the UnifiedDeploymentController class
}     // ← This closes the namespace