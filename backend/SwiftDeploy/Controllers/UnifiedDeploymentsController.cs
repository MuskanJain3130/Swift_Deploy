// Controllers/UnifiedDeploymentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Services;
using SwiftDeploy.Services.Interfaces;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Deployment = SwiftDeploy.Models.Deployment;
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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TokenService _tokenService;
        private readonly IMongoCollection<Deployment> _deploymentsCollection;

        public UnifiedDeploymentController(
            IUnifiedDeploymentService deploymentService,
            ITemplateEngine templateEngine,
            IConfiguration configuration,
            TokenService tokenService,
            ILogger<UnifiedDeploymentController> logger,
            IHttpContextAccessor httpContextAccessor,
            IMongoDatabase mongoDatabase)
        {
            _deploymentService = deploymentService;
            _templateEngine = templateEngine;
            _configuration = configuration;
            _tokenService = tokenService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _deploymentsCollection = mongoDatabase.GetCollection<Deployment>("Deployments");
        }

        //[HttpPost("deploy-without-github")]
        //[Authorize]
        //public async Task<IActionResult> DeployWithoutGitHub([FromForm] UploadProjectRequest request)
        //{
        //    var projectId = Guid.NewGuid().ToString();
        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        //    try
        //    {
        //        // Validate request
        //        if (!ModelState.IsValid)
        //            return BadRequest(ModelState);


        //        if (string.IsNullOrEmpty(userId))
        //            return Unauthorized("Invalid token");

        //        var platformToken = await _tokenService.GetPlatformTokenAsync(userId, request.Platform, HttpContext);


        //        var supportedPlatforms = new[] { "vercel", "cloudflare", "netlify" };
        //        if (!supportedPlatforms.Contains(request.Platform.ToLower()))
        //            return BadRequest($"Unsupported platform: {request.Platform}");

        //        // Initialize project tracking
        //        var projectInfo = new ProjectInfo
        //        {
        //            ProjectId = projectId,
        //            ProjectName = request.ProjectName,
        //            Description = request.Description,
        //            Platform = request.Platform.ToLower(),
        //            CreatedAt = DateTime.UtcNow,
        //            Status = DeploymentStatus.Uploading,
        //            Config = request.Config
        //        };
        //        _projects[projectId] = projectInfo;

        //        _logger.LogInformation($"Starting deployment for project {request.ProjectName} on {request.Platform}");

        //        // Step 1: Upload and extract project
        //        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Processing, "Extracting project files...");
        //        var localProjectPath = await _deploymentService.UploadAndExtractProjectAsync(request.ProjectZip, request.ProjectName);

        //        // Step 2: Create GitHub repo on SwiftDeploy account
        //        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.CreatingRepo, "Creating GitHub repository...");
        //        var repoName = await _deploymentService.CreateSwiftDeployRepoAsync(request.ProjectName, request.Description);
        //        projectInfo.GitHubRepoName = repoName;
        //        projectInfo.GitHubRepoUrl = $"https://github.com/swiftdeploy-repos/{repoName}";

        //        // Step 3: Push code to repo
        //        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.PushingCode, "Pushing code to repository...");
        //        await _deploymentService.PushCodeToSwiftDeployRepoAsync(repoName, localProjectPath);

        //        // Step 4: Generate and push config
        //        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.GeneratingConfig, "Generating deployment configuration...");
        //        await _deploymentService.PushConfigToRepoAsync(repoName, request.Platform, request.Config);

        //        // Step 5: Deploy to platform
        //        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Deploying, $"Deploying to {request.Platform}...");

        //        DeploymentResponse deploymentResult = request.Platform.ToLower() switch
        //        {
        //            "cloudflare" => await _deploymentService.DeployToCloudflareAsync(repoName, "main", request.Config, userId, platformToken),
        //            "netlify" => await _deploymentService.DeployToNetlifyAsync(repoName, "main", request.Config, userId, platformToken),
        //            "vercel" => await _deploymentService.DeployToVercelAsync(repoName, "main", request.Config, userId, platformToken),
        //            _ => throw new ArgumentException($"Unsupported platform: {request.Platform}")
        //        };

        //        if (deploymentResult.Success)
        //        {
        //            await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Completed, "Deployment completed successfully!");
        //            projectInfo.DeploymentUrl = deploymentResult.DeploymentUrl;
        //            projectInfo.Status = DeploymentStatus.Completed;

        //            // ⭐ Save successful deployment to MongoDB
        //            var mongoDeployment = new Deployment
        //            {
        //                UserId = userId,
        //                Platform = request.Platform.ToLower(),
        //                RepoId = projectInfo.GitHubRepoName,
        //                GitHubRepoUrl = projectInfo.GitHubRepoUrl,
        //                Status = "completed",
        //                ServiceUrl = deploymentResult.DeploymentUrl,
        //                ConfigFileUrl = deploymentResult.ConfigFileUrl,
        //                DeployedAt = DateTime.UtcNow
        //            };
        //            await _deploymentsCollection.InsertOneAsync(mongoDeployment);
        //            _logger.LogInformation($"✅ Deployment saved to MongoDB: {mongoDeployment.Id}");
        //        }
        //        else
        //        {
        //            await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, deploymentResult.Message);
        //            projectInfo.Status = DeploymentStatus.Failed;

        //            // ⭐ Save failed deployment to MongoDB
        //            var mongoDeployment = new Deployment
        //            {
        //                UserId = userId,
        //                Platform = request.Platform.ToLower(),
        //                RepoId = projectInfo.GitHubRepoName,
        //                GitHubRepoUrl = projectInfo.GitHubRepoUrl,
        //                Status = "failed",
        //                DeployedAt = DateTime.UtcNow
        //            };
        //            await _deploymentsCollection.InsertOneAsync(mongoDeployment);
        //        }

        //        return Ok(new DeploymentResponse
        //        {
        //            Success = deploymentResult.Success,
        //            Message = deploymentResult.Message,
        //            ProjectId = projectId,
        //            GitHubRepoUrl = projectInfo.GitHubRepoUrl,
        //            DeploymentUrl = deploymentResult.DeploymentUrl,
        //            ConfigFileUrl = deploymentResult.ConfigFileUrl,
        //            Status = projectInfo.Status
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Deployment failed for project {request.ProjectName}");
        //        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, ex.Message);

        //        // ⭐ Save error deployment to MongoDB
        //        try
        //        {
        //            var mongoDeployment = new Deployment
        //            {
        //                UserId = userId,
        //                Platform = request.Platform.ToLower(),
        //                Status = "failed",
        //                DeployedAt = DateTime.UtcNow
        //            };
        //            await _deploymentsCollection.InsertOneAsync(mongoDeployment);
        //        }
        //        catch { /* Ignore MongoDB errors in catch block */ }

        //        return StatusCode(500, new DeploymentResponse
        //        {
        //            Success = false,
        //            Message = $"Deployment failed: {ex.Message}",
        //            ProjectId = projectId,
        //            Status = DeploymentStatus.Failed
        //        });
        //    }
        //}


        // Controllers/UnifiedDeploymentController.cs

        [HttpPost("deploy-without-github")]
        [Authorize]
        public async Task<IActionResult> DeployWithoutGitHub([FromForm] UploadProjectRequest request)
        {
            var projectId = Guid.NewGuid().ToString();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string localProjectPath = null;

            try
            {
                // Validate request
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Invalid token");

                //// ⭐ Validate that either ProjectZip OR AzureBlobName is provided
                //if (request.ProjectZip == null && string.IsNullOrWhiteSpace(request.AzureBlobName))
                //    return BadRequest("Either ProjectZip file or AzureBlobName must be provided.");

                //if (request.ProjectZip != null && !string.IsNullOrWhiteSpace(request.AzureBlobName))
                //    return BadRequest("Provide either ProjectZip file OR AzureBlobName, not both.");

                var platformToken = request.Platform.ToLower() switch
                {
                    "cloudflare" => _configuration["SwiftDeploy:CloudFlareToken"],
                    //"netlify" => await _deploymentService.DeployToNetlifyAsync(repoName, "main", request.Config, userId, platformToken),
                    "vercel" => _configuration["SwiftDeploy:VercelToken"],
                    _ => throw new ArgumentException($"Unsupported platform: {request.Platform}")
                };

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

                // ⭐ Step 1: Upload/Download and extract project
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Processing, "Extracting project files...");

                if (!string.IsNullOrWhiteSpace(request.AzureBlobName))
                {
                    // ⭐ Download from Azure Blob Storage
                    _logger.LogInformation($"Downloading project from Azure blob: {request.AzureBlobName}");
                    localProjectPath = await _deploymentService.DownloadAndExtractFromAzureAsync(request.AzureBlobName, request.ProjectName);
                }
                //else
                //{
                //    // Upload from form file (existing logic)
                //    _logger.LogInformation($"Uploading project from form file");
                //    localProjectPath = await _deploymentService.UploadAndExtractProjectAsync(request.ProjectZip, request.ProjectName);
                //}

                // Step 2: Create GitHub repo on SwiftDeploy account
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.CreatingRepo, "Creating GitHub repository...");
                var repoName = await _deploymentService.CreateSwiftDeployRepoAsync(request.ProjectName, request.Description);
                projectInfo.GitHubRepoName = repoName;
                projectInfo.GitHubRepoUrl = $"https://github.com/swiftdeployapp/{repoName}";

                // Step 3: Push code to repo
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.PushingCode, "Pushing code to repository...");
                await _deploymentService.PushCodeToSwiftDeployRepoAsync(repoName, localProjectPath);

                // ⭐ Cleanup: Delete extracted folder after pushing to GitHub
                try
                {
                    if (Directory.Exists(localProjectPath))
                    {
                        Directory.Delete(localProjectPath, true);
                        _logger.LogInformation($"Deleted extracted project folder: {localProjectPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, $"Failed to cleanup extracted folder: {localProjectPath}");
                    // Don't fail the deployment for cleanup errors
                }

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

                    // Save successful deployment to MongoDB
                    var mongoDeployment = new Deployment
                    {
                        UserId = userId,
                        Platform = request.Platform.ToLower(),
                        RepoId = projectInfo.GitHubRepoName,
                        GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        Status = "completed",
                        ServiceUrl = deploymentResult.DeploymentUrl,
                        ConfigFileUrl = deploymentResult.ConfigFileUrl,
                        DeployedAt = DateTime.UtcNow
                    };
                    await _deploymentsCollection.InsertOneAsync(mongoDeployment);
                    _logger.LogInformation($"✅ Deployment saved to MongoDB: {mongoDeployment.Id}");
                }
                else
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, deploymentResult.Message);
                    projectInfo.Status = DeploymentStatus.Failed;

                    // Save failed deployment to MongoDB
                    var mongoDeployment = new Deployment
                    {
                        UserId = userId,
                        Platform = request.Platform.ToLower(),
                        RepoId = projectInfo.GitHubRepoName,
                        GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        Status = "failed",
                        DeployedAt = DateTime.UtcNow
                    };
                    await _deploymentsCollection.InsertOneAsync(mongoDeployment);
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

                // ⭐ Cleanup on error
                try
                {
                    if (!string.IsNullOrEmpty(localProjectPath) && Directory.Exists(localProjectPath))
                    {
                        Directory.Delete(localProjectPath, true);
                        _logger.LogInformation($"Cleaned up extracted folder after error: {localProjectPath}");
                    }
                }
                catch { /* Ignore cleanup errors */ }

                // Save error deployment to MongoDB
                try
                {
                    var mongoDeployment = new Deployment
                    {
                        UserId = userId,
                        Platform = request.Platform.ToLower(),
                        Status = "failed",
                        DeployedAt = DateTime.UtcNow
                    };
                    await _deploymentsCollection.InsertOneAsync(mongoDeployment);
                }
                catch { /* Ignore MongoDB errors in catch block */ }

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

                var supportedPlatforms = new[] { "vercel", "cloudflare", "netlify", "githubpages" };
                if (!supportedPlatforms.Contains(request.Platform.ToLower()))
                    return BadRequest($"Unsupported platform: {request.Platform}");

                // Get GitHub token from database
                var githubToken = await ((UnifiedDeploymentService)_deploymentService).GetGitHubTokenForUserAsync(request.UserId);

                if (string.IsNullOrEmpty(githubToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "GitHub token not found in database. Please connect your GitHub account first.",
                        platform = "github",
                        userId = request.UserId
                    });
                }

                _logger.LogInformation($"✅ GitHub token retrieved from database (length: {githubToken.Length})");

                // Get platform token (skip for GitHub Pages)
                string platformToken = null;
                if (request.Platform.ToLower() != "githubpages")
                {
                    platformToken = await _tokenService.GetPlatformTokenAsync(request.UserId, request.Platform, HttpContext);
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

                // Step 1: Generate and save config (skip for GitHub Pages)
                string configFileUrl = null;

                if (request.Platform.ToLower() != "githubpages")
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.GeneratingConfig, "Generating and saving configuration...");

                    var gitHubService = HttpContext.RequestServices.GetRequiredService<IGitHubService>();
                    var configResult = await gitHubService.GenerateAndSaveConfigAsync(
                        request.Platform,
                        request.GitHubRepo,
                        githubToken,
                        request.Branch ?? "main",
                        request.Config
                    );

                    if (!configResult.Success)
                    {
                        await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed,
                            $"Failed to save config: {configResult.Message}");

                        return BadRequest(new
                        {
                            success = false,
                            message = $"Failed to save config to GitHub: {configResult.Message}",
                            projectId = projectId,
                            gitHubRepoUrl = projectInfo.GitHubRepoUrl,
                            status = (int)DeploymentStatus.Failed
                        });
                    }

                    configFileUrl = configResult.FileUrl;
                    _logger.LogInformation($"✅ Config file saved: {configFileUrl}");
                }
                else
                {
                    _logger.LogInformation("Skipping config generation for GitHub Pages");
                }

                // Step 2: Deploy to platform
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Deploying, $"Deploying to {request.Platform}...");

                DeploymentResponse deploymentResult = request.Platform.ToLower() switch
                {
                    "cloudflare" => await DeployToCloudflareWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, platformToken, githubToken),
                    "netlify" => await DeployToNetlifyWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, platformToken, githubToken),
                    "vercel" => await DeployToVercelWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, platformToken, githubToken),
                    "githubpages" => await DeployToGitHubPagesWithUserRepo(request.GitHubRepo, request.Branch ?? "main", request.Config, githubToken),
                    _ => throw new ArgumentException($"Unsupported platform: {request.Platform}")
                };

                if (deploymentResult.Success)
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Completed, "Deployment completed successfully!");
                    projectInfo.DeploymentUrl = deploymentResult.DeploymentUrl;
                    projectInfo.Status = DeploymentStatus.Completed;

                    _logger.LogInformation($"✅ Deployment completed: {deploymentResult.DeploymentUrl}");

                    // ⭐ Save deployment to MongoDB
                    var mongoDeployment = new Deployment
                    {
                        UserId = request.UserId,
                        Platform = request.Platform.ToLower(),
                        RepoId = request.GitHubRepo,
                        GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        Status = "completed",
                        ServiceUrl = deploymentResult.DeploymentUrl,
                        ConfigFileUrl = configFileUrl,
                        DeployedAt = DateTime.UtcNow,
                        PlatformProjectId = deploymentResult.PlatformProjectId,
                        PlatformProjectName = deploymentResult.PlatformProjectName,
                        InternalProjectId = projectId
                    };
                    await _deploymentsCollection.InsertOneAsync(mongoDeployment);
                    _logger.LogInformation($"✅ Deployment saved to MongoDB: {mongoDeployment.Id}");

                    // ⭐ Return consistent response format
                    return Ok(new
                    {
                        success = true,
                        message = deploymentResult.Message,
                        projectId = projectId,
                        deploymentId = mongoDeployment.Id,
                        gitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        deploymentUrl = deploymentResult.DeploymentUrl,
                        configFileUrl = configFileUrl,
                        PlatformProjectId = deploymentResult.PlatformProjectId,
                        PlatformProjectName = deploymentResult.PlatformProjectName,
                        status = (int)DeploymentStatus.Completed,
                        progress = 100,
                        currentStep = "Completed"
                    });
                }
                else
                {
                    await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, deploymentResult.Message);
                    projectInfo.Status = DeploymentStatus.Failed;

                    _logger.LogError($"❌ Deployment failed: {deploymentResult.Message}");

                    // ⭐ Save failed deployment to MongoDB
                    var mongoDeployment = new Deployment
                    {
                        UserId = request.UserId,
                        Platform = request.Platform.ToLower(),
                        RepoId = request.GitHubRepo,
                        GitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        Status = "failed",
                        DeployedAt = DateTime.UtcNow
                    };
                    await _deploymentsCollection.InsertOneAsync(mongoDeployment);

                    return BadRequest(new
                    {
                        success = false,
                        message = deploymentResult.Message,
                        projectId = projectId,
                        deploymentId = mongoDeployment.Id,
                        gitHubRepoUrl = projectInfo.GitHubRepoUrl,
                        status = (int)DeploymentStatus.Failed
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GitHub deployment failed for {request.GitHubRepo}");
                await _deploymentService.UpdateProjectStatusAsync(projectId, DeploymentStatus.Failed, ex.Message);

                // ⭐ Save error deployment to MongoDB
                try
                {
                    var mongoDeployment = new Deployment
                    {
                        UserId = request.UserId,
                        Platform = request.Platform.ToLower(),
                        RepoId = request.GitHubRepo,
                        Status = "failed",
                        DeployedAt = DateTime.UtcNow
                    };
                    await _deploymentsCollection.InsertOneAsync(mongoDeployment);
                }
                catch { /* Ignore MongoDB errors in catch block */ }

                return StatusCode(500, new
                {
                    success = false,
                    message = $"Deployment failed: {ex.Message}",
                    projectId = projectId,
                    status = (int)DeploymentStatus.Failed
                });
            }
        }
        //[HttpGet("projects")]
        //public async Task<IActionResult> GetUserProjects([FromQuery] string userId = null)
        //{
        //    try
        //    {
        //        // If no userId provided, return all projects (for admin/testing)
        //        // In production, you'd get userId from authentication context
        //        var userProjects = _projects.Values
        //            .Where(p => userId == null || p.ProjectId.Contains(userId)) // Simple filter for demo
        //            .OrderByDescending(p => p.CreatedAt)
        //            .Select(p => new
        //            {
        //                p.ProjectId,
        //                p.ProjectName,
        //                p.Description,
        //                p.Platform,
        //                p.Status,
        //                p.CreatedAt,
        //                p.GitHubRepoUrl,
        //                p.DeploymentUrl,
        //                StatusMessage = GetStatusMessage(p.Status),
        //                Progress = GetProgressPercentage(p.Status)
        //            })
        //            .ToList();

        //        return Ok(new
        //        {
        //            projects = userProjects,
        //            total = userProjects.Count
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving user projects");
        //        return StatusCode(500, "Error retrieving projects");
        //    }
        //}

        //[HttpGet("projects/{projectId}")]
        //public async Task<IActionResult> GetProjectDetails(string projectId)
        //{
        //    try
        //    {
        //        if (!_projects.TryGetValue(projectId, out var project))
        //            return NotFound("Project not found");

        //        return Ok(new
        //        {
        //            project.ProjectId,
        //            project.ProjectName,
        //            project.Description,
        //            project.Platform,
        //            project.Status,
        //            project.CreatedAt,
        //            project.GitHubRepoName,
        //            project.GitHubRepoUrl,
        //            project.DeploymentUrl,
        //            project.Config,
        //            StatusMessage = GetStatusMessage(project.Status),
        //            Progress = GetProgressPercentage(project.Status),
        //            CurrentStep = GetCurrentStep(project.Status)
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error retrieving project details for {projectId}");
        //        return StatusCode(500, "Error retrieving project details");
        //    }
        //}// Helper function for Cloudflare deployment
        //private async Task<DeploymentResponse> DeployToCloudflareWithConfig(string repoPath, string branch, CommonConfig config)
        //{
        //    try
        //    {
        //        var swiftDeployToken = _configuration["SwiftDeploy:CloudflareToken"];
        //        if (string.IsNullOrEmpty(swiftDeployToken))
        //            throw new Exception("SwiftDeploy Cloudflare token not configured");

        //        using var client = new HttpClient();
        //        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", swiftDeployToken);

        //        // Get account ID
        //        var userResponse = await client.GetAsync("https://api.cloudflare.com/client/v4/accounts");
        //        var userString = await userResponse.Content.ReadAsStringAsync();
        //        if (!userResponse.IsSuccessStatusCode)
        //            throw new Exception($"Failed to get Cloudflare account: {userString}");

        //        var userJson = JsonDocument.Parse(userString);
        //        string accountId = userJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

        //        // Generate project name
        //        string projectName = GenerateCloudflareProjectName(repoPath, branch);

        //        // Create project
        //        var createProjectUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects";
        //        var createPayload = new
        //        {
        //            name = projectName,
        //            production_branch = branch,
        //            source = new
        //            {
        //                type = "github",
        //                config = new
        //                {
        //                    git_provider = "github",
        //                    owner = "swiftdeploy-repos", // Your organization
        //                    repo_name = repoPath.Split('/').Last(),
        //                    branch = branch
        //                }
        //            },
        //            build_config = new
        //            {
        //                build_command = config.BuildCommand,
        //                destination_dir = config.OutputDirectory,
        //                root_dir = ""
        //            }
        //        };

        //        var createContent = new StringContent(JsonSerializer.Serialize(createPayload), System.Text.Encoding.UTF8, "application/json");
        //        var createResponse = await client.PostAsync(createProjectUrl, createContent);
        //        var createResult = await createResponse.Content.ReadAsStringAsync();

        //        if (!createResponse.IsSuccessStatusCode)
        //            throw new Exception($"Cloudflare project creation failed: {createResult}");

        //        var createData = JsonDocument.Parse(createResult);
        //        var deploymentUrl = createData.RootElement.GetProperty("result").GetProperty("subdomain").GetString() + ".pages.dev";

        //        // Trigger deployment
        //        var deployUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}/deployments";
        //        var deployResponse = await client.PostAsync(deployUrl, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        //        return new DeploymentResponse
        //        {
        //            Success = deployResponse.IsSuccessStatusCode,
        //            Message = deployResponse.IsSuccessStatusCode ? "Cloudflare deployment started successfully" : "Cloudflare deployment failed",
        //            DeploymentUrl = $"https://{deploymentUrl}"
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new DeploymentResponse
        //        {
        //            Success = false,
        //            Message = $"Cloudflare deployment error: {ex.Message}"
        //        };
        //    }
        //}

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
        private async Task<DeploymentResponse> DeployToCloudflareWithUserRepo(string repoPath, string branch, CommonConfig config, string cloudflareToken, string githubToken)
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

                _logger.LogInformation($"✅ Cloudflare Account ID: {accountId}");

                // Generate project name
                string projectName = GenerateCloudflareProjectName(repoPath, branch);
                _logger.LogInformation($"✅ Generated Cloudflare project name: {projectName}");

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
                        build_command = config?.BuildCommand ?? "",
                        destination_dir = config?.OutputDirectory ?? ".",
                        root_dir = ""
                    }
                };

                var createContent = new StringContent(JsonSerializer.Serialize(createPayload), System.Text.Encoding.UTF8, "application/json");
                var createResponse = await client.PostAsync(createProjectUrl, createContent);
                var createResult = await createResponse.Content.ReadAsStringAsync();

                _logger.LogInformation($"Cloudflare create project response: {createResult}");

                if (!createResponse.IsSuccessStatusCode)
                    throw new Exception($"Cloudflare project creation failed: {createResult}");

                var createData = JsonDocument.Parse(createResult);
                var result = createData.RootElement.GetProperty("result");

                // ⭐ Extract project details with null checks
                string subdomain = null;
                string deploymentUrl = null;

                // Try to get subdomain
                if (result.TryGetProperty("subdomain", out var subdomainProp) &&
                    subdomainProp.ValueKind != JsonValueKind.Null)
                {
                    subdomain = subdomainProp.GetString();
                    deploymentUrl = $"https://{subdomain}.pages.dev";
                }

                // ⭐ Extract project ID (Cloudflare uses project name as ID)
                string cloudflareProjectId = projectName;
                string cloudflareProjectName = projectName;

                // ⭐ Try to get canonical_deployment URL if available (with null checks)
                if (result.TryGetProperty("canonical_deployment", out var canonicalDeployment) &&
                    canonicalDeployment.ValueKind == JsonValueKind.Object)
                {
                    if (canonicalDeployment.TryGetProperty("url", out var canonicalUrl) &&
                        canonicalUrl.ValueKind != JsonValueKind.Null)
                    {
                        var canonicalUrlString = canonicalUrl.GetString();
                        if (!string.IsNullOrEmpty(canonicalUrlString))
                        {
                            deploymentUrl = canonicalUrlString;
                            _logger.LogInformation($"✅ Using canonical deployment URL: {deploymentUrl}");
                        }
                    }
                }

                // ⭐ Fallback: If no deployment URL yet, use subdomain
                if (string.IsNullOrEmpty(deploymentUrl) && !string.IsNullOrEmpty(subdomain))
                {
                    deploymentUrl = $"https://{subdomain}.pages.dev";
                }

                _logger.LogInformation($"✅ Cloudflare project created: {projectName}");
                _logger.LogInformation($"✅ Cloudflare project ID: {cloudflareProjectId}");
                _logger.LogInformation($"✅ Cloudflare deployment URL: {deploymentUrl}");

                // Trigger deployment
                var deployUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}/deployments";
                var deployResponse = await client.PostAsync(deployUrl, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
                var deployBody = await deployResponse.Content.ReadAsStringAsync();

                _logger.LogInformation($"Cloudflare trigger deployment response: {deployBody}");

                if (deployResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Cloudflare deployment triggered successfully");

                    // ⭐ Parse deployment response for additional details (with null checks)
                    try
                    {
                        var deployData = JsonDocument.Parse(deployBody);
                        if (deployData.RootElement.TryGetProperty("result", out var deployResult) &&
                            deployResult.ValueKind == JsonValueKind.Object)
                        {
                            if (deployResult.TryGetProperty("url", out var deployUrlProp) &&
                                deployUrlProp.ValueKind != JsonValueKind.Null)
                            {
                                var specificDeploymentUrl = deployUrlProp.GetString();
                                if (!string.IsNullOrEmpty(specificDeploymentUrl))
                                {
                                    // Ensure URL has https://
                                    if (!specificDeploymentUrl.StartsWith("http"))
                                    {
                                        specificDeploymentUrl = $"https://{specificDeploymentUrl}";
                                    }
                                    deploymentUrl = specificDeploymentUrl;
                                    _logger.LogInformation($"✅ Updated deployment URL from deployment response: {deploymentUrl}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse deployment response, using project URL");
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ Cloudflare deployment trigger returned non-success: {deployBody}");
                }

                // ⭐ Final fallback: If still no deployment URL, construct from project name
                if (string.IsNullOrEmpty(deploymentUrl))
                {
                    deploymentUrl = $"https://{projectName}.pages.dev";
                    _logger.LogInformation($"⚠️ Using fallback deployment URL: {deploymentUrl}");
                }

                // ⭐ Return complete response with project details
                return new DeploymentResponse
                {
                    Success = true,
                    Message = "Cloudflare deployment started successfully",
                    DeploymentUrl = deploymentUrl,
                    GitHubRepoUrl = $"https://github.com/{repoPath}",
                    PlatformProjectId = cloudflareProjectId,      // ⭐ Project name (used as ID)
                    PlatformProjectName = cloudflareProjectName,  // ⭐ Project name
                    ProjectId = cloudflareProjectId               // ⭐ For backward compatibility
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying to Cloudflare");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Cloudflare deployment error: {ex.Message}"
                };
            }
        }

        private async Task<DeploymentResponse> DeployToVercelWithUserRepo(
      string repoPath,
      string branch,
      CommonConfig config,
      string vercelToken,
      string githubToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoPath))
                    return new DeploymentResponse { Success = false, Message = "Repository path missing" };

                branch ??= "main";

                // Build request payload
                var repoParts = repoPath.Split('/');
                if (repoParts.Length < 2)
                    return new DeploymentResponse { Success = false, Message = "Repository path must be in format 'owner/repo'" };

                var owner = repoParts[0];
                var repoName = repoParts[1];

                // ⭐ STEP 1: Fetch RepoId and CommitSha from GitHub API
                long repoId = 0;
                string commitSha = null;

                try
                {
                    using var githubClient = new HttpClient();
                    githubClient.DefaultRequestHeaders.UserAgent.ParseAdd("SwiftDeploy/1.0");

                    if (!string.IsNullOrWhiteSpace(githubToken))
                    {
                        githubClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);
                    }

                    // Get repository info (for repoId)
                    var repoUrl = $"https://api.github.com/repos/{owner}/{repoName}";
                    var repoResponse = await githubClient.GetAsync(repoUrl);

                    if (!repoResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await repoResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to fetch GitHub repo info: {Error}", errorBody);
                        return new DeploymentResponse
                        {
                            Success = false,
                            Message = $"Failed to fetch repository info from GitHub: {errorBody}"
                        };
                    }

                    var repoJson = await repoResponse.Content.ReadAsStringAsync();
                    var repoDoc = JsonDocument.Parse(repoJson);
                    repoId = repoDoc.RootElement.GetProperty("id").GetInt64();

                    _logger.LogInformation("✅ GitHub Repo ID: {RepoId}", repoId);

                    // Get latest commit SHA for the branch
                    var branchUrl = $"https://api.github.com/repos/{owner}/{repoName}/branches/{branch}";
                    var branchResponse = await githubClient.GetAsync(branchUrl);

                    if (!branchResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await branchResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to fetch branch info: {Error}", errorBody);
                        return new DeploymentResponse
                        {
                            Success = false,
                            Message = $"Failed to fetch branch '{branch}' from GitHub: {errorBody}"
                        };
                    }

                    var branchJson = await branchResponse.Content.ReadAsStringAsync();
                    var branchDoc = JsonDocument.Parse(branchJson);
                    commitSha = branchDoc.RootElement
                        .GetProperty("commit")
                        .GetProperty("sha")
                        .GetString();

                    _logger.LogInformation("✅ Latest Commit SHA: {CommitSha}", commitSha);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching GitHub repository information");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to fetch GitHub info: {ex.Message}"
                    };
                }

                // ⭐ STEP 2: Build request payload with RepoId and CommitSha
                var requestData = new
                {
                    Owner = owner,
                    RepoName = repoName,
                    Branch = branch,
                    RepoId = repoId,  // ⭐ Added
                    CommitSha = commitSha,  // ⭐ Added
                    BuildCommand = string.IsNullOrWhiteSpace(config?.BuildCommand) ? null : config.BuildCommand,
                    BuildDir = string.IsNullOrWhiteSpace(config?.OutputDirectory) ? null : config.OutputDirectory,
                    InstallCommand = string.IsNullOrWhiteSpace(config?.InstallCommand) ? null : config.InstallCommand,
                    Framework = string.IsNullOrWhiteSpace(config?.Framework) ? null : config.Framework,
                    TeamId = string.IsNullOrWhiteSpace(config?.TeamId?.ToString()) ? "" : config.TeamId.ToString(),
                    EnvironmentVariables = config?.EnvironmentVariables?
                        .Select(kv => new { name = kv.Key, value = kv.Value })
                        .ToArray() ?? Array.Empty<object>()
                };

                // ⭐ STEP 3: Call internal Vercel controller
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var initiateUrl = $"{baseUrl}/api/vercel/initiate";

                using var client = new HttpClient();

                // Add Vercel API token
                if (!string.IsNullOrWhiteSpace(vercelToken))
                    client.DefaultRequestHeaders.Add("Vercel-Api-Token", vercelToken);
                else
                    return new DeploymentResponse { Success = false, Message = "Vercel token is required" };

                // Add GitHub token
                if (!string.IsNullOrWhiteSpace(githubToken))
                    client.DefaultRequestHeaders.Add("GitHub-Api-Token", githubToken);

                var json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Sending to Vercel controller:\n{Payload}", json);

                var resp = await client.PostAsync(initiateUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                var body = await resp.Content.ReadAsStringAsync();

                _logger.LogInformation("Vercel controller response ({Status}):\n{Body}", resp.StatusCode, body);

                if (!resp.IsSuccessStatusCode)
                {
                    // Extract error message
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("error", out var error))
                        {
                            return new DeploymentResponse
                            {
                                Success = false,
                                Message = $"Vercel error: {error.GetString()}"
                            };
                        }
                        if (doc.RootElement.TryGetProperty("message", out var msg))
                        {
                            return new DeploymentResponse
                            {
                                Success = false,
                                Message = $"Vercel error: {msg.GetString()}"
                            };
                        }
                    }
                    catch { /* ignore parse errors */ }

                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Vercel deployment failed: {body}"
                    };
                }

                // ⭐ STEP 4: Parse success response and extract deployment URL
            // ... existing code ...

                // ⭐ STEP 4: Parse success response and extract BOTH deployment URL AND project ID
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    string deploymentUrl = null;
                    string projectName = null;
                    string vercelProjectId = null; // ⭐ NEW

                    // Extract project name
                    if (root.TryGetProperty("projectName", out var projNameProp))
                    {
                        projectName = projNameProp.GetString();
                    }

                    // ⭐ Extract Vercel project ID
                    if (root.TryGetProperty("projectId", out var projIdProp))
                    {
                        vercelProjectId = projIdProp.GetString();
                    }

                    // Try to get deploymentUrl directly
                    if (root.TryGetProperty("deploymentUrl", out var deployProp))
                    {
                        deploymentUrl = deployProp.GetString();
                    }
                    else if (root.TryGetProperty("deploymentInfo", out var depInfo) &&
                             depInfo.ValueKind == JsonValueKind.Object &&
                             depInfo.TryGetProperty("url", out var urlProp))
                    {
                        deploymentUrl = urlProp.GetString();
                    }

                    // If we have projectName but no URL, construct it
                    if (string.IsNullOrEmpty(deploymentUrl) && !string.IsNullOrEmpty(projectName))
                    {
                        deploymentUrl = $"https://{projectName}.vercel.app";
                    }

                    // Ensure URL has https://
                    if (!string.IsNullOrEmpty(deploymentUrl))
                    {
                        if (!deploymentUrl.StartsWith("http"))
                        {
                            deploymentUrl = $"https://{deploymentUrl}";
                        }

                        if (!deploymentUrl.EndsWith("/"))
                        {
                            deploymentUrl += "/";
                        }
                    }

                    _logger.LogInformation("✅ Vercel deployment URL: {Url}", deploymentUrl);
                    _logger.LogInformation("✅ Vercel project ID: {ProjectId}", vercelProjectId);
                    _logger.LogInformation("✅ Vercel project name: {ProjectName}", projectName);

                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Vercel deployment initiated successfully",
                        DeploymentUrl = deploymentUrl,
                        GitHubRepoUrl = $"https://github.com/{repoPath}",
                        ProjectId = root.TryGetProperty("projectId", out var pid) ? pid.GetString() : null,
                        PlatformProjectId = vercelProjectId, // ⭐ NEW
                        PlatformProjectName = projectName     // ⭐ NEW
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Vercel response");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Vercel deployment initiated (response parsing failed)",
                        DeploymentUrl = null,
                        GitHubRepoUrl = $"https://github.com/{repoPath}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Vercel controller");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Vercel deployment error: {ex.Message}"
                };
            }
        
        }
        // Deploy to GitHub Pages using USER'S GitHub repo
        private async Task<DeploymentResponse> DeployToGitHubPagesWithUserRepo(
     string repoPath,
     string branch,
     CommonConfig config,
     string githubToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoPath))
                    return new DeploymentResponse { Success = false, Message = "Repository path missing" };

                branch ??= "main";

                var repoParts = repoPath.Split('/');
                if (repoParts.Length < 2)
                    return new DeploymentResponse { Success = false, Message = "Repository path must be in format 'owner/repo'" };

                var owner = repoParts[0];
                var repoName = repoParts[1];

                _logger.LogInformation("Deploying {Repo} to GitHub Pages on branch {Branch}", repoPath, branch);

                // ⭐ STEP 1: Fetch GitHub repository ID
                long githubRepoId = 0;
                try
                {
                    using var githubClient = new HttpClient();
                    githubClient.DefaultRequestHeaders.UserAgent.ParseAdd("SwiftDeploy/1.0");
                    githubClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);

                    var repoUrl = $"https://api.github.com/repos/{owner}/{repoName}";
                    var repoResponse = await githubClient.GetAsync(repoUrl);

                    if (repoResponse.IsSuccessStatusCode)
                    {
                        var repoJson = await repoResponse.Content.ReadAsStringAsync();
                        var repoDoc = JsonDocument.Parse(repoJson);
                        githubRepoId = repoDoc.RootElement.GetProperty("id").GetInt64();
                        _logger.LogInformation("✅ GitHub Repository ID: {RepoId}", githubRepoId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to fetch GitHub repo ID, using fallback identifier");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching GitHub repo ID, using fallback identifier");
                }

                // ⭐ Map framework to valid GitHub Pages build_type
                string buildType = MapFrameworkToBuildType(config?.Framework);
                _logger.LogInformation($"Mapped framework '{config?.Framework}' to build_type '{buildType}'");

                // Call internal GitHub Pages controller
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var enableUrl = $"{baseUrl}/api/githubpages/enable";

                using var client = new HttpClient();

                if (!string.IsNullOrWhiteSpace(githubToken))
                    client.DefaultRequestHeaders.Add("GitHub-Token", githubToken);
                else
                    return new DeploymentResponse { Success = false, Message = "GitHub token is required" };

                var requestData = new
                {
                    Owner = owner,
                    Repo = repoName,
                    Branch = branch,
                    Path = config?.OutputDirectory ?? "/",
                    BuildType = buildType,
                    TriggerBuild = true
                };

                var json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Sending to GitHub Pages controller:\n{Payload}", json);

                var resp = await client.PostAsync(enableUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                var body = await resp.Content.ReadAsStringAsync();

                _logger.LogInformation("GitHub Pages controller response ({Status}):\n{Body}", resp.StatusCode, body);

                if (!resp.IsSuccessStatusCode)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("message", out var msg))
                        {
                            return new DeploymentResponse
                            {
                                Success = false,
                                Message = $"GitHub Pages error: {msg.GetString()}"
                            };
                        }
                    }
                    catch { /* ignore parse errors */ }

                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"GitHub Pages deployment failed: {body}"
                    };
                }

                // Construct GitHub Pages URL
                var deploymentUrl = $"https://{owner}.github.io/{repoName}/";

                // ⭐ STEP 2: Create unique project identifier
                // Use GitHub repo ID if available, otherwise use formatted repo path
                string githubPagesProjectId;
                if (githubRepoId > 0)
                {
                    githubPagesProjectId = $"gh-{githubRepoId}"; // e.g., "gh-123456789"
                }
                else
                {
                    githubPagesProjectId = $"gh-pages-{owner}-{repoName}".ToLower(); // Fallback
                }

                _logger.LogInformation("✅ GitHub Pages deployment URL: {Url}", deploymentUrl);
                _logger.LogInformation("✅ GitHub Pages project ID: {ProjectId}", githubPagesProjectId);

                return new DeploymentResponse
                {
                    Success = true,
                    Message = "GitHub Pages deployment initiated successfully",
                    DeploymentUrl = deploymentUrl,
                    GitHubRepoUrl = $"https://github.com/{repoPath}",
                    PlatformProjectId = githubPagesProjectId,    // ⭐ FIXED - unique GitHub repo ID
                    PlatformProjectName = repoName,              // ⭐ Just the repo name
                    ProjectId = githubPagesProjectId             // ⭐ FIXED - unique identifier
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GitHub Pages controller");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"GitHub Pages deployment error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Map framework/config values to valid GitHub Pages build_type
        /// </summary>
        private string MapFrameworkToBuildType(string framework)
        {
            if (string.IsNullOrWhiteSpace(framework))
            {
                return "legacy";  // Default to legacy for static sites
            }

            // Normalize to lowercase for comparison
            var normalizedFramework = framework.ToLower().Trim();

            // Map common framework values to GitHub Pages build types
            return normalizedFramework switch
            {
                // ⭐ Explicit GitHub Pages build types
                "legacy" => "legacy",
                "workflow" => "workflow",

                // ⭐ Static site frameworks → legacy
                "static" => "legacy",
                "html" => "legacy",
                "vanilla" => "legacy",
                "plain" => "legacy",

                // ⭐ Modern frameworks with build workflows → workflow
                "nextjs" => "workflow",
                "next" => "workflow",
                "react" => "workflow",
                "vue" => "workflow",
                "angular" => "workflow",
                "svelte" => "workflow",
                "gatsby" => "workflow",
                "nuxt" => "workflow",
                "vite" => "workflow",

                // ⭐ Static site generators → workflow (they have build steps)
                "jekyll" => "workflow",
                "hugo" => "workflow",
                "11ty" => "workflow",
                "eleventy" => "workflow",
                "astro" => "workflow",
                "docusaurus" => "workflow",

                // ⭐ Default fallback
                _ => "legacy"
            };
        }
        // ============================================
        // DELETE DEPLOYMENT METHODS FOR ALL PLATFORMS
        // ============================================

        /// <summary>
        /// Delete deployment from any platform
        /// </summary>
        // ============================================
        // DELETE DEPLOYMENT METHODS FOR ALL PLATFORMS
        // ============================================

        /// <summary>
        /// Delete deployment from any platform
        /// </summary>
        /// <summary>
        /// Delete deployment from any platform AND MongoDB
        /// </summary>
        [HttpDelete("deployments/{identifier}")]
        public async Task<IActionResult> DeleteDeployment(string identifier)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Invalid token");

                _logger.LogInformation($"Delete request for identifier: {identifier}");

                ProjectInfo project = null;
                Deployment mongoDeployment = null;

                // ⭐ STEP 1: Check MongoDB FIRST (not memory)
                _logger.LogInformation($"Checking MongoDB for deployment...");

                // Build filter conditionally based on identifier format
                FilterDefinition<Deployment> filter;

                // Check if identifier is a valid MongoDB ObjectId (24 hex characters)
                bool isValidObjectId = identifier.Length == 24 &&
                    System.Text.RegularExpressions.Regex.IsMatch(identifier, "^[0-9a-fA-F]{24}$");

                if (isValidObjectId)
                {
                    // If it's a valid ObjectId, include it in the filter
                    filter = Builders<Deployment>.Filter.And(
                        Builders<Deployment>.Filter.Eq(d => d.UserId, userId),
                        Builders<Deployment>.Filter.Or(
                            Builders<Deployment>.Filter.Eq(d => d.InternalProjectId, identifier),
                            Builders<Deployment>.Filter.Eq(d => d.Id, identifier),
                            Builders<Deployment>.Filter.Eq(d => d.PlatformProjectId, identifier)
                        )
                    );
                }
                else
                {
                    // If it's NOT a valid ObjectId (like "prj_xxx"), exclude the Id field
                    filter = Builders<Deployment>.Filter.And(
                        Builders<Deployment>.Filter.Eq(d => d.UserId, userId),
                        Builders<Deployment>.Filter.Or(
                            Builders<Deployment>.Filter.Eq(d => d.InternalProjectId, identifier),
                            Builders<Deployment>.Filter.Eq(d => d.PlatformProjectId, identifier)
                        )
                    );
                }

                mongoDeployment = await _deploymentsCollection.Find(filter).FirstOrDefaultAsync();

                if (mongoDeployment != null)
                {
                    _logger.LogInformation($"✅ Found deployment in MongoDB: {mongoDeployment.Id}");
                    _logger.LogInformation($"✅ Platform: {mongoDeployment.Platform}");
                    _logger.LogInformation($"✅ Platform Project ID: {mongoDeployment.PlatformProjectId}");
                    _logger.LogInformation($"✅ Platform Project Name: {mongoDeployment.PlatformProjectName}");

                    // Create ProjectInfo from MongoDB data
                    project = new ProjectInfo
                    {
                        ProjectId = mongoDeployment.InternalProjectId ?? mongoDeployment.Id,
                        ProjectName = mongoDeployment.PlatformProjectName,
                        Platform = mongoDeployment.Platform,
                        DeploymentUrl = mongoDeployment.ServiceUrl,
                        GitHubRepoName = mongoDeployment.RepoId,
                        GitHubRepoUrl = mongoDeployment.GitHubRepoUrl,
                        PlatformProjectId = mongoDeployment.PlatformProjectId,
                        PlatformProjectName = mongoDeployment.PlatformProjectName
                    };
                }
                // ⭐ STEP 2: Fallback to memory (for deployments in progress)
                else if (_projects.TryGetValue(identifier, out var memoryProject))
                {
                    project = memoryProject;
                    _logger.LogInformation($"✅ Found project in memory by API ID: {identifier}");
                }
                // ⭐ STEP 3: Not found anywhere
                else
                {
                    _logger.LogWarning($"Deployment not found: {identifier}");

                    return NotFound(new
                    {
                        success = false,
                        message = "Deployment not found in database or memory",
                        identifier = identifier,
                        hint = "Make sure you're using the correct project ID, deployment ID, or platform project ID"
                    });
                }

                _logger.LogInformation($"Deleting deployment for project {identifier} on {project.Platform}");

                // Get tokens
                var githubToken = await ((UnifiedDeploymentService)_deploymentService)
                    .GetGitHubTokenForUserAsync(userId);

                string platformToken = null;
                if (project.Platform != "githubpages")
                {
                    platformToken = await _tokenService.GetPlatformTokenAsync(userId, project.Platform, HttpContext);

                    if (string.IsNullOrEmpty(platformToken))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"No {project.Platform} token found. Cannot delete deployment."
                        });
                    }
                }

                // ⭐ STEP 4: Delete from platform using stored platform project ID
                DeploymentResponse deleteResult;

                if (!string.IsNullOrEmpty(project.PlatformProjectId))
                {
                    _logger.LogInformation($"Using stored platform project ID for deletion: {project.PlatformProjectId}");

                    deleteResult = project.Platform.ToLower() switch
                    {
                        "vercel" => await DeleteVercelProjectById(project.PlatformProjectId, platformToken),
                        "cloudflare" => await DeleteCloudflareProjectById(project.PlatformProjectId, platformToken),
                        "netlify" => await DeleteNetlifyProjectById(project.PlatformProjectId, platformToken),
                        "githubpages" => await DeleteGitHubPagesDeployment(project, githubToken),
                        _ => new DeploymentResponse { Success = false, Message = $"Unsupported platform: {project.Platform}" }
                    };
                }
                else
                {
                    _logger.LogWarning("No platform project ID stored, falling back to search method");

                    // Fallback to old method (search by name)
                    deleteResult = project.Platform.ToLower() switch
                    {
                        "vercel" => await DeleteVercelDeployment(project, platformToken),
                        "cloudflare" => await DeleteCloudflareDeployment(project, platformToken),
                        "netlify" => await DeleteNetlifyDeployment(project, platformToken),
                        "githubpages" => await DeleteGitHubPagesDeployment(project, githubToken),
                        _ => new DeploymentResponse { Success = false, Message = $"Unsupported platform: {project.Platform}" }
                    };
                }

                // ⭐ STEP 5: If platform deletion successful, delete from MongoDB and memory
                if (deleteResult.Success)
                {
                    // Remove from memory tracking (if exists)
                    if (_projects.TryRemove(identifier, out _))
                    {
                        _logger.LogInformation($"✅ Removed deployment from memory: {identifier}");
                    }

                    // ⭐ Delete from MongoDB
                    if (mongoDeployment != null)
                    {
                        var deleteMongoResult = await _deploymentsCollection.DeleteOneAsync(d => d.Id == mongoDeployment.Id);

                        if (deleteMongoResult.DeletedCount > 0)
                        {
                            _logger.LogInformation($"✅ Deployment deleted from MongoDB: {mongoDeployment.Id}");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ MongoDB deletion returned 0 deleted count for: {mongoDeployment.Id}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No MongoDB record to delete (deployment was only in memory)");
                    }

                    _logger.LogInformation($"✅ Deployment fully deleted for {identifier}");

                    return Ok(new
                    {
                        success = true,
                        message = deleteResult.Message,
                        identifier = identifier,
                        platform = project.Platform,
                        platformProjectId = project.PlatformProjectId,
                        deletedFromMongoDB = mongoDeployment != null,
                        mongoDeploymentId = mongoDeployment?.Id
                    });
                }
                else
                {
                    // ⭐ Platform deletion failed - don't delete from MongoDB
                    _logger.LogError($"❌ Platform deletion failed, keeping MongoDB record: {deleteResult.Message}");

                    return BadRequest(new
                    {
                        success = false,
                        message = deleteResult.Message,
                        identifier = identifier,
                        platform = project.Platform,
                        platformProjectId = project.PlatformProjectId,
                        hint = "Platform deletion failed. MongoDB record preserved for retry."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting deployment for {identifier}");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error deleting deployment: {ex.Message}",
                    identifier = identifier
                });
            }
        }
        // ⭐ Direct deletion using platform project ID
        private async Task<DeploymentResponse> DeleteVercelProjectById(string vercelProjectId, string vercelToken)
        {
            try
            {
                _logger.LogInformation($"Deleting Vercel project by ID: {vercelProjectId}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", vercelToken);

                var deleteUrl = $"https://api.vercel.com/v9/projects/{vercelProjectId}";

                _logger.LogInformation($"DELETE request to: {deleteUrl}");

                var response = await client.DeleteAsync(deleteUrl);
                var body = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Vercel API response status: {response.StatusCode}");
                _logger.LogInformation($"Vercel API response body: {body}");

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation($"✅ Vercel project deleted successfully: {vercelProjectId}");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = $"Vercel project '{vercelProjectId}' deleted successfully"
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Vercel project not found: {vercelProjectId}");
                    return new DeploymentResponse
                    {
                        Success = true, // Consider it success if already deleted
                        Message = "Vercel project not found (may have been deleted already)"
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = "Access denied. You don't have permission to delete this Vercel project."
                    };
                }
                else
                {
                    // Try to parse error message
                    string errorMessage = body;
                    try
                    {
                        var errorDoc = JsonDocument.Parse(body);
                        if (errorDoc.RootElement.TryGetProperty("error", out var errorProp))
                        {
                            if (errorProp.TryGetProperty("message", out var msgProp))
                            {
                                errorMessage = msgProp.GetString();
                            }
                        }
                    }
                    catch { /* Ignore JSON parse errors */ }

                    _logger.LogError($"Failed to delete Vercel project: {errorMessage}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to delete Vercel project: {errorMessage}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Vercel project by ID");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error deleting Vercel project: {ex.Message}"
                };
            }
        }

        private async Task<DeploymentResponse> DeleteCloudflareProjectById(string cloudflareProjectName, string cloudflareToken)
        {
            try
            {
                _logger.LogInformation($"Deleting Cloudflare project: {cloudflareProjectName}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cloudflareToken);

                // Get account ID
                var accountResponse = await client.GetAsync("https://api.cloudflare.com/client/v4/accounts");
                var accountBody = await accountResponse.Content.ReadAsStringAsync();

                if (!accountResponse.IsSuccessStatusCode)
                {
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to get Cloudflare account: {accountBody}"
                    };
                }

                var accountJson = JsonDocument.Parse(accountBody);
                var accountId = accountJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

                // Delete using project name (Cloudflare uses project name, not ID)
                var deleteUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{cloudflareProjectName}";
                var deleteResponse = await client.DeleteAsync(deleteUrl);
                var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

                if (deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Cloudflare project deleted: {cloudflareProjectName}");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Cloudflare deployment deleted successfully"
                    };
                }
                else
                {
                    _logger.LogError($"Failed to delete Cloudflare project: {deleteBody}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to delete Cloudflare deployment: {deleteBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Cloudflare deployment");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error deleting Cloudflare deployment: {ex.Message}"
                };
            }
        }

        private async Task<DeploymentResponse> DeleteNetlifyProjectById(string netlifySiteId, string netlifyToken)
        {
            try
            {
                _logger.LogInformation($"Deleting Netlify site by ID: {netlifySiteId}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", netlifyToken);

                var deleteUrl = $"https://api.netlify.com/api/v1/sites/{netlifySiteId}";
                var deleteResponse = await client.DeleteAsync(deleteUrl);

                if (deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Netlify site deleted: {netlifySiteId}");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Netlify deployment deleted successfully"
                    };
                }
                else
                {
                    var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete Netlify site: {deleteBody}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to delete Netlify deployment: {deleteBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Netlify deployment");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error deleting Netlify deployment: {ex.Message}"
                };
            }
        }

        // ============================================
        // VERCEL DELETE
        // ============================================
        // ============================================
        // VERCEL DELETE - COMPLETE IMPLEMENTATION
        // ============================================

        /// <summary>
        /// Delete Vercel deployment by querying Vercel API to find the project
        /// </summary>
        private async Task<DeploymentResponse> DeleteVercelDeployment(ProjectInfo project, string vercelToken)
        {
            try
            {
                _logger.LogInformation($"Deleting Vercel project: {project.ProjectName}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", vercelToken);

                // Step 1: Try to extract project name from deployment URL
                var vercelProjectName = ExtractVercelProjectName(project);

                if (string.IsNullOrEmpty(vercelProjectName))
                {
                    _logger.LogWarning("Could not extract Vercel project name from deployment URL");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = "Could not determine Vercel project name. Please delete manually from Vercel dashboard."
                    };
                }

                _logger.LogInformation($"Extracted Vercel project name: {vercelProjectName}");

                // Step 2: Query Vercel API to find the project ID
                var vercelProjectId = await GetVercelProjectIdByName(vercelProjectName, vercelToken);

                if (string.IsNullOrEmpty(vercelProjectId))
                {
                    _logger.LogWarning($"Vercel project '{vercelProjectName}' not found in your account");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Vercel project '{vercelProjectName}' not found. It may have been deleted already or you don't have access to it."
                    };
                }

                _logger.LogInformation($"Found Vercel project ID: {vercelProjectId}");

                // Step 3: Delete the project using the found ID
                var deleteUrl = $"https://api.vercel.com/v9/projects/{vercelProjectId}";

                _logger.LogInformation($"Deleting Vercel project at: {deleteUrl}");

                var response = await client.DeleteAsync(deleteUrl);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation($"✅ Vercel project deleted successfully: {vercelProjectName} (ID: {vercelProjectId})");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = $"Vercel deployment '{vercelProjectName}' deleted successfully"
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Vercel project not found (may be already deleted): {vercelProjectName}");
                    return new DeploymentResponse
                    {
                        Success = true, // Consider it success if already deleted
                        Message = "Vercel deployment not found (may have been deleted already)"
                    };
                }
                else
                {
                    _logger.LogError($"Failed to delete Vercel project: {body}");

                    // Try to parse error message
                    string errorMessage = body;
                    try
                    {
                        var errorDoc = JsonDocument.Parse(body);
                        if (errorDoc.RootElement.TryGetProperty("error", out var errorProp))
                        {
                            if (errorProp.TryGetProperty("message", out var msgProp))
                            {
                                errorMessage = msgProp.GetString();
                            }
                        }
                    }
                    catch { /* Ignore JSON parse errors */ }

                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to delete Vercel deployment: {errorMessage}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Vercel deployment");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error deleting Vercel deployment: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Extract Vercel project name from deployment URL or project info
        /// </summary>
        private string ExtractVercelProjectName(ProjectInfo project)
        {
            try
            {
                // Method 1: Extract from deployment URL
                if (!string.IsNullOrEmpty(project.DeploymentUrl))
                {
                    var uri = new Uri(project.DeploymentUrl);
                    var host = uri.Host;

                    // Format: https://project-name.vercel.app or https://project-name-hash.vercel.app
                    if (host.EndsWith(".vercel.app"))
                    {
                        var projectName = host.Replace(".vercel.app", "");
                        _logger.LogInformation($"Extracted project name from URL: {projectName}");
                        return projectName;
                    }

                    // Format: Custom domain - try to use project name instead
                    _logger.LogWarning($"Deployment URL uses custom domain: {host}");
                }

                // Method 2: Use GitHub repo name as fallback
                if (!string.IsNullOrEmpty(project.GitHubRepoName))
                {
                    var repoParts = project.GitHubRepoName.Split('/');
                    if (repoParts.Length >= 2)
                    {
                        var repoName = repoParts[1].ToLower().Replace("_", "-").Replace(" ", "-");
                        _logger.LogInformation($"Using GitHub repo name as fallback: {repoName}");
                        return repoName;
                    }
                }

                // Method 3: Use project name as last resort
                if (!string.IsNullOrEmpty(project.ProjectName))
                {
                    var sanitizedName = project.ProjectName.ToLower()
                        .Replace("_", "-")
                        .Replace(" ", "-")
                        .Replace(".", "-");
                    _logger.LogInformation($"Using sanitized project name: {sanitizedName}");
                    return sanitizedName;
                }

                _logger.LogWarning("Could not extract Vercel project name from any source");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting Vercel project name");
                return null;
            }
        }

        /// <summary>
        /// Query Vercel API to find project ID by name
        /// </summary>
        private async Task<string> GetVercelProjectIdByName(string projectName, string vercelToken)
        {
            try
            {
                _logger.LogInformation($"Querying Vercel API for project: {projectName}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", vercelToken);

                // List all projects (paginated)
                var listUrl = "https://api.vercel.com/v9/projects?limit=100";
                var response = await client.GetAsync(listUrl);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to list Vercel projects: {body}");
                    return null;
                }

                var result = JsonDocument.Parse(body);

                if (!result.RootElement.TryGetProperty("projects", out var projectsArray))
                {
                    _logger.LogWarning("No 'projects' property in Vercel API response");
                    return null;
                }

                // Search for matching project
                foreach (var proj in projectsArray.EnumerateArray())
                {
                    if (proj.TryGetProperty("name", out var nameProp))
                    {
                        var name = nameProp.GetString();

                        // Exact match
                        if (name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (proj.TryGetProperty("id", out var idProp))
                            {
                                var projectId = idProp.GetString();
                                _logger.LogInformation($"✅ Found exact match - Project: {name}, ID: {projectId}");
                                return projectId;
                            }
                        }

                        // Partial match (in case Vercel added suffix)
                        if (name.StartsWith(projectName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (proj.TryGetProperty("id", out var idProp))
                            {
                                var projectId = idProp.GetString();
                                _logger.LogInformation($"✅ Found partial match - Project: {name}, ID: {projectId}");
                                return projectId;
                            }
                        }
                    }
                }

                _logger.LogWarning($"No matching Vercel project found for: {projectName}");

                // Log available projects for debugging
                _logger.LogInformation("Available Vercel projects:");
                foreach (var proj in projectsArray.EnumerateArray())
                {
                    if (proj.TryGetProperty("name", out var nameProp))
                    {
                        _logger.LogInformation($"  - {nameProp.GetString()}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Vercel projects");
                return null;
            }
        }

        // ============================================
        // CLOUDFLARE DELETE
        // ============================================
        private async Task<DeploymentResponse> DeleteCloudflareDeployment(ProjectInfo project, string cloudflareToken)
        {
            try
            {
                _logger.LogInformation($"Deleting Cloudflare project: {project.ProjectName}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cloudflareToken);

                // Get account ID
                var accountResponse = await client.GetAsync("https://api.cloudflare.com/client/v4/accounts");
                var accountBody = await accountResponse.Content.ReadAsStringAsync();

                if (!accountResponse.IsSuccessStatusCode)
                {
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to get Cloudflare account: {accountBody}"
                    };
                }

                var accountJson = JsonDocument.Parse(accountBody);
                var accountId = accountJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

                // Extract project name from deployment URL or stored data
                var projectName = ExtractCloudflareProjectName(project);

                // Delete project
                var deleteUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}";
                var deleteResponse = await client.DeleteAsync(deleteUrl);
                var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

                if (deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Cloudflare project deleted: {projectName}");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Cloudflare deployment deleted successfully"
                    };
                }
                else
                {
                    _logger.LogError($"Failed to delete Cloudflare project: {deleteBody}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to delete Cloudflare deployment: {deleteBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Cloudflare deployment");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error deleting Cloudflare deployment: {ex.Message}"
                };
            }
        }

        // ============================================
        // NETLIFY DELETE
        // ============================================
        private async Task<DeploymentResponse> DeleteNetlifyDeployment(ProjectInfo project, string netlifyToken)
        {
            try
            {
                _logger.LogInformation($"Deleting Netlify site: {project.ProjectName}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", netlifyToken);

                // Extract site ID from deployment URL or stored data
                var siteId = ExtractNetlifySiteId(project);

                if (string.IsNullOrEmpty(siteId))
                {
                    // Try to find site by name
                    var sitesResponse = await client.GetAsync("https://api.netlify.com/api/v1/sites");
                    var sitesBody = await sitesResponse.Content.ReadAsStringAsync();

                    if (sitesResponse.IsSuccessStatusCode)
                    {
                        var sitesJson = JsonDocument.Parse(sitesBody);
                        foreach (var site in sitesJson.RootElement.EnumerateArray())
                        {
                            var siteName = site.GetProperty("name").GetString();
                            if (siteName.Contains(project.ProjectName, StringComparison.OrdinalIgnoreCase))
                            {
                                siteId = site.GetProperty("id").GetString();
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(siteId))
                {
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = "Could not find Netlify site ID"
                    };
                }

                // Delete site
                var deleteUrl = $"https://api.netlify.com/api/v1/sites/{siteId}";
                var deleteResponse = await client.DeleteAsync(deleteUrl);

                if (deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Netlify site deleted: {siteId}");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Netlify deployment deleted successfully"
                    };
                }
                else
                {
                    var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete Netlify site: {deleteBody}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to delete Netlify deployment: {deleteBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Netlify deployment");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error deleting Netlify deployment: {ex.Message}"
                };
            }
        }

        // ============================================
        // GITHUB PAGES DELETE
        // ============================================
        private async Task<DeploymentResponse> DeleteGitHubPagesDeployment(ProjectInfo project, string githubToken)
        {
            try
            {
                _logger.LogInformation($"Disabling GitHub Pages for: {project.GitHubRepoName}");

                var repoParts = project.GitHubRepoName.Split('/');
                if (repoParts.Length != 2)
                {
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = "Invalid repository format"
                    };
                }

                var owner = repoParts[0];
                var repo = repoParts[1];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SwiftDeploy/1.0");
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                // Delete GitHub Pages site
                var deleteUrl = $"https://api.github.com/repos/{owner}/{repo}/pages";
                var deleteResponse = await client.DeleteAsync(deleteUrl);

                if (deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation($"✅ GitHub Pages disabled for: {owner}/{repo}");
                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "GitHub Pages deployment disabled successfully"
                    };
                }
                else
                {
                    var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to disable GitHub Pages: {deleteBody}");
                    return new DeploymentResponse
                    {
                        Success = false,
                        Message = $"Failed to disable GitHub Pages: {deleteBody}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling GitHub Pages");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Error disabling GitHub Pages: {ex.Message}"
                };
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private string ExtractCloudflareProjectName(ProjectInfo project)
        {
            // Try to extract from deployment URL
            // Format: https://project-name.pages.dev
            if (!string.IsNullOrEmpty(project.DeploymentUrl))
            {
                var uri = new Uri(project.DeploymentUrl);
                var host = uri.Host;
                if (host.EndsWith(".pages.dev"))
                {
                    return host.Replace(".pages.dev", "");
                }
            }

            // Fallback to project name
            return project.ProjectName?.ToLower().Replace(" ", "-") ?? "unknown";
        }

        private string ExtractNetlifySiteId(ProjectInfo project)
        {
            // Try to extract from deployment URL
            // Format: https://site-id.netlify.app or https://custom-domain.com
            if (!string.IsNullOrEmpty(project.DeploymentUrl))
            {
                var uri = new Uri(project.DeploymentUrl);
                var host = uri.Host;
                if (host.EndsWith(".netlify.app"))
                {
                    return host.Replace(".netlify.app", "");
                }
            }

            // If ProjectId was stored as Netlify site ID
            return project.ProjectId;
        }
    } // ← This closes the UnifiedDeploymentController class
}     // ← This closes the namespace