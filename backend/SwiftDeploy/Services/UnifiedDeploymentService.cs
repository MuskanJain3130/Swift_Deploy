﻿// Services/UnifiedDeploymentService.cs
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Services.Interfaces;
using System.IO.Compression;
using System.Text.Json;
using FileMode = System.IO.FileMode;

namespace SwiftDeploy.Services.Interfaces
{
    public class UnifiedDeploymentService : IUnifiedDeploymentService
    {
        private readonly IConfiguration _configuration;
        private readonly ITemplateEngine _templateEngine;
        private readonly ILogger<UnifiedDeploymentService> _logger;
        private readonly GitHubClient _gitHubClient;
        private static readonly Dictionary<string, ProjectInfo> _projectStatuses = new();

        public UnifiedDeploymentService(
            IConfiguration configuration,
            ITemplateEngine templateEngine,
            ILogger<UnifiedDeploymentService> logger)
        {
            _configuration = configuration;
            _templateEngine = templateEngine;
            _logger = logger;

            // Initialize GitHub client with SwiftDeploy token
            var swiftDeployToken = _configuration["SwiftDeploy:GitHubToken"];
            _gitHubClient = new GitHubClient(new ProductHeaderValue("SwiftDeploy"))
            {
                Credentials = new Credentials(swiftDeployToken)
            };
        }

        public async Task<string> UploadAndExtractProjectAsync(IFormFile zipFile, string projectName)
        {
            try
            {
                // Create temp directory for extraction
                var tempDir = Path.Combine(Path.GetTempPath(), "swiftdeploy", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Save uploaded file
                var zipPath = Path.Combine(tempDir, "project.zip");
                using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                {
                    await zipFile.CopyToAsync(stream);
                }

                // Extract ZIP file
                var extractDir = Path.Combine(tempDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                _logger.LogInformation($"Project {projectName} extracted to {extractDir}");
                return extractDir;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting project {projectName}");
                throw;
            }
        }

        public async Task<string> CreateSwiftDeployRepoAsync(string projectName, string description = null)
        {
            try
            {
                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeploy-repos";
                var repoName = GenerateRepoName(projectName);

                var newRepo = new NewRepository(repoName)
                {
                    Description = description ?? $"SwiftDeploy project: {projectName}",
                    Private = false,
                    AutoInit = true
                };

                // Create repo in organization
                var repo = await _gitHubClient.Repository.Create(orgName, newRepo);

                _logger.LogInformation($"Created GitHub repo: {repo.FullName}");
                return repo.Name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating GitHub repo for {projectName}");
                throw;
            }
        }

        public async Task<bool> PushCodeToSwiftDeployRepoAsync(string repoName, string localProjectPath)
        {
            try
            {
                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeploy-repos";

                // Get all files from local project
                var files = Directory.GetFiles(localProjectPath, "*", SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    var relativePath = Path.GetRelativePath(localProjectPath, filePath);
                    var content = await File.ReadAllTextAsync(filePath);

                    try
                    {
                        // Try to get existing file
                        var existingFile = await _gitHubClient.Repository.Content.GetAllContents(orgName, repoName, relativePath);

                        // Update existing file
                        var updateRequest = new UpdateFileRequest($"Update {relativePath}", content, existingFile[0].Sha);
                        await _gitHubClient.Repository.Content.UpdateFile(orgName, repoName, relativePath, updateRequest);
                    }
                    catch (NotFoundException)
                    {
                        // Create new file
                        var createRequest = new CreateFileRequest($"Add {relativePath}", content);
                        await _gitHubClient.Repository.Content.CreateFile(orgName, repoName, relativePath, createRequest);
                    }
                }

                _logger.LogInformation($"Successfully pushed code to {orgName}/{repoName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error pushing code to repo {repoName}");
                return false;
            }
        }

        public async Task<bool> PushConfigToRepoAsync(string repoName, string platform, CommonConfig config)
        {
            try
            {
                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeploy-repos";

                // Generate config content
                var configContent = await _templateEngine.GenerateConfigAsync(platform, config);
                var fileName = _templateEngine.GetConfigFileName(platform);

                try
                {
                    // Try to get existing config file
                    var existingFile = await _gitHubClient.Repository.Content.GetAllContents(orgName, repoName, fileName);

                    // Update existing config
                    var updateRequest = new UpdateFileRequest($"Update {platform} configuration", configContent, existingFile[0].Sha);
                    await _gitHubClient.Repository.Content.UpdateFile(orgName, repoName, fileName, updateRequest);
                }
                catch (NotFoundException)
                {
                    // Create new config file
                    var createRequest = new CreateFileRequest($"Add {platform} configuration", configContent);
                    await _gitHubClient.Repository.Content.CreateFile(orgName, repoName, fileName, createRequest);
                }

                _logger.LogInformation($"Successfully pushed {platform} config to {orgName}/{repoName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error pushing config to repo {repoName}");
                return false;
            }
        }
        public async Task<DeploymentResponse> DeployToCloudflareAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken)
        {
            try
            {
                // Use platformToken instead of configuration token
                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception("Cloudflare token not provided");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", platformToken);

                // Get account ID
                var userResponse = await client.GetAsync("https://api.cloudflare.com/client/v4/accounts");
                var userString = await userResponse.Content.ReadAsStringAsync();
                if (!userResponse.IsSuccessStatusCode)
                    throw new Exception($"Failed to get Cloudflare account: {userString}");

                var userJson = JsonDocument.Parse(userString);
                string accountId = userJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

                // Generate project name
                string projectName = GenerateCloudflareProjectName(repoName, branch);
                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeploy-repos";

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
                            owner = orgName,
                            repo_name = repoName,
                            production_branch = branch
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
                _logger.LogError(ex, $"Cloudflare deployment error for {repoName}");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Cloudflare deployment error: {ex.Message}"
                };
            }
        }
        public async Task<DeploymentResponse> DeployToNetlifyAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken)
        {
            try
            {
                // Use platformToken instead of configuration token
                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception("Netlify token not provided");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", platformToken);

                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeploy-repos";

                var sitePayload = new
                {
                    name = $"swiftdeploy-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    repo = new
                    {
                        provider = "github",
                        repo = $"{orgName}/{repoName}",
                        branch = branch
                    },
                    build_settings = new
                    {
                        cmd = config.BuildCommand,
                        dir = config.OutputDirectory,
                        env = config.EnvironmentVariables
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
                _logger.LogError(ex, $"Netlify deployment error for {repoName}");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Netlify deployment error: {ex.Message}"
                };
            }
        }
        public async Task<DeploymentResponse> DeployToVercelAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken)
        {
            try
            {
                // Use platformToken instead of configuration token
                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception("Vercel token not provided");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", platformToken);

                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeploy-repos";

                var payload = new
                {
                    name = $"swiftdeploy-{Guid.NewGuid().ToString()[..8]}",
                    gitRepository = new
                    {
                        type = "github",
                        repo = $"{orgName}/{repoName}",
                        ref_ = branch
                    },
                    buildCommand = config.BuildCommand,
                    outputDirectory = config.OutputDirectory,
                    installCommand = config.InstallCommand,
                    env = config.EnvironmentVariables?.Select(kv => new { key = kv.Key, value = kv.Value }).ToArray()
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
                _logger.LogError(ex, $"Vercel deployment error for {repoName}");
                return new DeploymentResponse
                {
                    Success = false,
                    Message = $"Vercel deployment error: {ex.Message}"
                };
            }
        }
        public async Task<ProjectInfo> GetProjectInfoAsync(string projectId)
        {
            try
            {
                if (_projectStatuses.TryGetValue(projectId, out var projectInfo))
                {
                    _logger.LogInformation($"Retrieved project info for {projectId}");
                    return projectInfo;
                }

                _logger.LogWarning($"Project {projectId} not found");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving project info for {projectId}");
                throw;
            }
        }
        public async Task UpdateProjectStatusAsync(string projectId, Models.DeploymentStatus status, string message = null)
        {
            try
            {
                if (_projectStatuses.TryGetValue(projectId, out var projectInfo))
                {
                    projectInfo.Status = status;
                    _projectStatuses[projectId] = projectInfo;

                    _logger.LogInformation($"Updated project {projectId} status to {status}" +
                                         (message != null ? $" with message: {message}" : ""));
                }
                else
                {
                    _logger.LogWarning($"Attempted to update status for non-existent project {projectId}");
                }

                // This is async to allow for future enhancements like database updates or notifications
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating project status for {projectId}");
                throw;
            }
        }// Helper method to generate repository name
        private string GenerateRepoName(string projectName)
        {
            // Sanitize project name for GitHub repo naming rules
            var sanitized = projectName.ToLower()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace(".", "-");

            // Remove invalid characters
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^a-z0-9\-]", "");

            // Add timestamp to ensure uniqueness
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return $"{sanitized}-{timestamp}";
        }

        // Helper method to generate Cloudflare project name
        private string GenerateCloudflareProjectName(string repo, string branch)
        {
            string raw = $"{repo}-{branch}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return $"swiftdeploy-{hex.Substring(0, 10)}";
        }
    }
}