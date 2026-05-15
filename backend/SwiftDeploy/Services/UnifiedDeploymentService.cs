// Services/UnifiedDeploymentService.cs
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Azure.Storage.Blobs;
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Models.SwiftDeploy.Models;
using SwiftDeploy.Services;
using SwiftDeploy.Services.Interfaces;
using System.IO.Compression;
using System.Text;
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
        private readonly MongoDbService _mongoDbService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly Dictionary<string, ProjectInfo> _projectStatuses = new();

        public UnifiedDeploymentService(
        IConfiguration configuration,
        ITemplateEngine templateEngine,
        ILogger<UnifiedDeploymentService> logger,
        MongoDbService mongoDbService,
        IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _templateEngine = templateEngine;
            _logger = logger;
            _mongoDbService = mongoDbService;
            _httpContextAccessor = httpContextAccessor;

            // Initialize GitHub client with SwiftDeploy token
            var swiftDeployToken = _configuration["SwiftDeploy:GitHubToken"];

            if (string.IsNullOrEmpty(swiftDeployToken))
            {
                _logger.LogError("❌ SwiftDeploy GitHub token is missing in configuration!");
                throw new Exception("SwiftDeploy:GitHubToken is not configured in appsettings.json");
            }

            _gitHubClient = new GitHubClient(new ProductHeaderValue("SwiftDeploy"))
            {
                Credentials = new Credentials(swiftDeployToken)
            };

            _logger.LogInformation("✅ GitHub client initialized with SwiftDeploy token");
        }



        // ⭐ Helper: Get user's GitHub token from header OR database
        private async Task<string?> GetUserGitHubTokenAsync(string userId)
        {
            try
            {
                // First, try to get from header/cookie
                var httpContext = _httpContextAccessor?.HttpContext;
                if (httpContext != null)
                {
                    // Check for GitHub token in cookies
                    if (httpContext.Request.Cookies.TryGetValue("GitHubAccessToken", out var cookieToken))
                    {
                        _logger.LogInformation("Retrieved GitHub token from cookie for user {UserId}", userId);
                        return cookieToken;
                    }

                    // Check for GitHub token in custom header
                    if (httpContext.Request.Headers.TryGetValue("X-GitHub-Token", out var headerToken))
                    {
                        _logger.LogInformation("Retrieved GitHub token from header for user {UserId}", userId);
                        return headerToken.ToString();
                    }
                }

                // ⭐ If not in header/cookie, get from database
                var filter = Builders<UserTokens>.Filter.And(
                    Builders<UserTokens>.Filter.Eq(t => t.UserId, userId)
                //Builders<UserTokens>.Filter.Eq(t => t.GitHubToken, "githubToken")
                );

                var userToken = await _mongoDbService.UserTokens.Find(filter).FirstOrDefaultAsync();

                if (userToken != null && !string.IsNullOrEmpty(userToken.GitHubToken))
                {
                    _logger.LogInformation("Retrieved GitHub token from database for user {UserId}", userId);
                    return userToken.GitHubToken;
                }

                _logger.LogWarning("No GitHub token found for user {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving GitHub token for user {UserId}", userId);
                return null;
            }
        }

        // ⭐ Helper: Get platform token (Cloudflare/Netlify/Vercel) from header OR database
        //private async Task<string?> GetPlatformTokenAsync(string userId, string platform)
        //{
        //    try
        //    {
        //        // First, try to get from header/cookie
        //        var httpContext = _httpContextAccessor?.HttpContext;
        //        if (httpContext != null)
        //        {
        //            // Check for platform token in cookies
        //            var cookieKey = $"{platform}AccessToken";
        //            if (httpContext.Request.Cookies.TryGetValue(cookieKey, out var cookieToken))
        //            {
        //                _logger.LogInformation("Retrieved {Platform} token from cookie for user {UserId}", platform, userId);
        //                return cookieToken;
        //            }

        //            // Check for platform token in custom header
        //            var headerKey = $"X-{platform}-Token";
        //            if (httpContext.Request.Headers.TryGetValue(headerKey, out var headerToken))
        //            {
        //                _logger.LogInformation("Retrieved {Platform} token from header for user {UserId}", platform, userId);
        //                return headerToken.ToString();
        //            }
        //        }

        //        // ⭐ If not in header/cookie, get from database
        //        var filter = Builders<UserTokens>.Filter.And(
        //            Builders<UserTokens>.Filter.Eq(t => t.UserId, userId),
        //            Builders<UserTokens>.Filter.Eq(t => t.GitHubToken, platform.ToLower())
        //        );

        //        var userToken = await _mongoDbService.UserTokens.Find(filter).FirstOrDefaultAsync();

        //        if (userToken != null && !string.IsNullOrEmpty(userToken.GitHubToken))
        //        {
        //            _logger.LogInformation("Retrieved {Platform} token from database for user {UserId}", platform, userId);
        //            return userToken.GitHubToken;
        //        }

        //        _logger.LogWarning("No {Platform} token found for user {UserId}", platform, userId);
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving {Platform} token for user {UserId}", platform, userId);
        //        return null;
        //    }
        //}
        private async Task<string?> GetPlatformTokenAsync(string userId, string platform)
        {
            try
            {
                var httpContext = _httpContextAccessor?.HttpContext;

                // ✅ 1. Try cookies/header (existing logic)
                if (httpContext != null)
                {
                    var cookieKey = $"{platform}AccessToken";
                    if (httpContext.Request.Cookies.TryGetValue(cookieKey, out var cookieToken))
                    {
                        _logger.LogInformation("Retrieved {Platform} token from cookie", platform);
                        return cookieToken;
                    }

                    var headerKey = $"X-{platform}-Token";
                    if (httpContext.Request.Headers.TryGetValue(headerKey, out var headerToken))
                    {
                        return headerToken.ToString();
                    }
                }

                // ✅ 2. FALLBACK → DATABASE (THIS FIXES WORKER)
                var user = await _mongoDbService.UserTokens
                    .Find(x => x.UserId == userId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("No user token record found for {UserId}", userId);
                    return null;
                }

                return platform.ToLower() switch
                {
                    "vercel" => user.VercelToken,
                    "netlify" => user.NetlifyToken,
                    "cloudflare" => user.CloudflareToken,
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {Platform} token", platform);
                return null;
            }
        }
        // Helper: create GitHubClient for a user (falls back to the service-level client if no token)
        private GitHubClient CreateGitHubClientForUser(string? userToken)
        {
            if (!string.IsNullOrEmpty(userToken))
            {
                return new GitHubClient(new ProductHeaderValue("SwiftDeploy"))
                {
                    Credentials = new Credentials(userToken)
                };
            }

            // fallback to service-level client (may be unauthenticated or use SwiftDeploy token)
            return _gitHubClient;
        }


        public async Task<string> CreateSwiftDeployRepoAsync(string projectName, string description = null)
        {
            try
            {
                //var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeployapp";
                var orgName = (await _gitHubClient.User.Current()).Login;
                var repoName = GenerateRepoName(projectName);

                var newRepo = new NewRepository(repoName)
                {
                    Description = description ?? $"SwiftDeploy project: {projectName}",
                    Private = false,
                    AutoInit = true
                };

                // Create repo in organization
                var repo = await _gitHubClient.Repository.Create(newRepo);

                _logger.LogInformation($"Created GitHub repo: {repo.FullName}");
                return repo.Name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating GitHub repo for {projectName}");
                throw;
            }
        }
        private string GetActualProjectRoot(string extractedPath)
        {
            try
            {
                var dirs = Directory.GetDirectories(extractedPath);
                var files = Directory.GetFiles(extractedPath);

                // If only 1 subdirectory and no files in root, dive into that directory
                if (dirs.Length == 1 && files.Length == 0)
                {
                    var singleSubDir = dirs[0];
                    _logger.LogInformation($"Found single wrapper directory: {singleSubDir}, diving in...");

                    // Recursively check if we need to go deeper
                    return GetActualProjectRoot(singleSubDir);
                }

                // Otherwise, this is the actual project root
                _logger.LogInformation($"Actual project root: {extractedPath}");
                return extractedPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining project root");
                return extractedPath; // Fallback to original path
            }
        }

        public async Task<bool> PushCodeToSwiftDeployRepoAsync(string repoName, string localProjectPath)
        {
            try
            {
                //var swiftDeployToken = _configuration["SwiftDeploy:GitHubToken"];

                //_gitHubClient = new GitHubClient(new ProductHeaderValue("SwiftDeploy"))
                //{
                //    Credentials = new Credentials(swiftDeployToken)
                //};
                var orgName = (await _gitHubClient.User.Current()).Login;

                // ⭐ FIX: Find the actual project root (skip empty wrapper directories)
                var projectRoot = GetActualProjectRoot(localProjectPath);

                _logger.LogInformation($"Pushing code from {projectRoot} to {orgName}/{repoName}");

                // Get all files from the actual project root
                var files = Directory.GetFiles(projectRoot, "*", SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    // ⭐ Calculate relative path from the ACTUAL project root
                    var relativePath = Path.GetRelativePath(projectRoot, filePath);
                    var content = await File.ReadAllTextAsync(filePath);

                    try
                    {
                        // Try to get existing file
                        var existingFile = await _gitHubClient.Repository.Content.GetAllContents(orgName, repoName, relativePath);

                        // Update existing file
                        var updateRequest = new UpdateFileRequest($"Update {relativePath}", content, existingFile[0].Sha);
                        await _gitHubClient.Repository.Content.UpdateFile(orgName, repoName, relativePath, updateRequest);

                        _logger.LogInformation($"Updated file: {relativePath}");
                    }
                    catch (NotFoundException)
                    {
                        // Create new file
                        var createRequest = new CreateFileRequest($"Add {relativePath}", content);
                        await _gitHubClient.Repository.Content.CreateFile(orgName, repoName, relativePath, createRequest);

                        _logger.LogInformation($"Created file: {relativePath}");
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

                var orgName = (await _gitHubClient.User.Current()).Login;

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

        // ⭐ Updated: Now retrieves token from database if not provided
        public async Task<DeploymentResponse> DeployToCloudflareAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken = null)
        {
            try
            {
                // ⭐ Get token from database if not provided
                if (string.IsNullOrEmpty(platformToken))
                {
                    platformToken = await GetPlatformTokenAsync(userId, "cloudflare");
                }

                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception("Cloudflare token not found. Please connect your Cloudflare account.");

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

        // ⭐ Updated: Now retrieves token from database if not provided
        public async Task<DeploymentResponse> DeployToNetlifyAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken = null)
        {
            try
            {
                // ⭐ Get token from database if not provided
                if (string.IsNullOrEmpty(platformToken))
                {
                    platformToken = await GetPlatformTokenAsync(userId, "netlify");
                }

                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception("Netlify token not found. Please connect your Netlify account.");

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

        // ⭐ Updated: Now retrieves token from database if not provided
        public async Task<DeploymentResponse> DeployToVercelAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken = null)
        {
            try
            {
                // ⭐ Get token from database if not provided
                if (string.IsNullOrEmpty(platformToken))
                {
                    platformToken = await GetPlatformTokenAsync(userId, "vercel");
                }

                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception("Vercel token not found. Please connect your Vercel account.");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", platformToken);

                var orgName = _configuration["SwiftDeploy:GitHubOrg"] ?? "swiftdeployapp";

                var payload = new
                {
                    name = $"swiftdeploy-{Guid.NewGuid().ToString()[..8]}",
                    gitRepository = new
                    {
                        type = "github",
                        repo = $"{orgName}/{repoName}",
                        @ref = branch
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

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating project status for {projectId}");
                throw;
            }
        }

        // Helper method to generate repository name
        private string GenerateRepoName(string projectName)
        {
            var sanitized = projectName.ToLower()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace(".", "-");

            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^a-z0-9\-]", "");

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
        public async Task<string> DownloadAndExtractFromAzureAsync(string blobName, string projectName)
        {
            try
            {
                var connectionString = _configuration["Azure:ConnectionString"];
                var containerName = _configuration["Azure:ContainerName"];

                if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
                    throw new Exception("Azure storage configuration missing (Azure:ConnectionString or Azure:ContainerName).");

                // Create temp directory for download and extraction
                var tempDir = Path.Combine(Path.GetTempPath(), "swiftdeploy", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var localZipPath = Path.Combine(tempDir, blobName);
                var extractDir = Path.Combine(tempDir, "extracted");

                try
                {
                    // Download blob to local file
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    var exists = await blobClient.ExistsAsync();
                    if (!exists.Value)
                    {
                        throw new FileNotFoundException($"Blob '{blobName}' not found in container '{containerName}'.");
                    }

                    _logger.LogInformation($"Downloading blob {blobName} from Azure...");
                    await blobClient.DownloadToAsync(localZipPath);

                    // Extract ZIP file
                    Directory.CreateDirectory(extractDir);
                    _logger.LogInformation($"Extracting {blobName} to {extractDir}...");
                    ZipFile.ExtractToDirectory(localZipPath, extractDir);

                    // ⭐ Delete blob from Azure after successful download
                    _logger.LogInformation($"Deleting blob {blobName} from Azure storage...");
                    await blobClient.DeleteIfExistsAsync();

                    // ⭐ Delete local zip file (keep only extracted folder)
                    if (File.Exists(localZipPath))
                    {
                        File.Delete(localZipPath);
                        _logger.LogInformation($"Deleted local zip file: {localZipPath}");
                    }

                    _logger.LogInformation($"Project {projectName} downloaded and extracted from Azure to {extractDir}");
                    return extractDir;
                }
                catch (InvalidDataException ide)
                {
                    _logger.LogError(ide, $"Failed to extract zip file: {localZipPath}");

                    // Cleanup on failure
                    if (File.Exists(localZipPath))
                        File.Delete(localZipPath);
                    if (Directory.Exists(extractDir))
                        Directory.Delete(extractDir, true);

                    throw new Exception("The downloaded file is not a valid zip archive.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading and extracting from Azure for project {projectName}");
                throw;
            }
        }
        // ⭐ Public method to get GitHub token (for use in controllers)
        public async Task<string?> GetGitHubTokenForUserAsync(string userId)
        {
            return await GetUserGitHubTokenAsync(userId);
        }

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
                var baseUri = new Uri(_configuration["BaseUrl"]);
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
                    $"{_configuration["BaseUrl"]}/api/netlify/deploy",
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
                var deploymentUrl = createData.RootElement.GetProperty("result").GetProperty("subdomain").GetString();

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

        private async Task<DeploymentResponse> DeployToVercelWithUserRepo(
        string repoPath,
        string branch,
        CommonConfig config,
        string vercelToken,
        string githubToken,
        string platformId = null)
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
                        .ToArray() ?? Array.Empty<object>(),
                    PlatformId = platformId
                };

                // ⭐ STEP 3: Call internal Vercel controller
                var baseUrl = _configuration["BaseUrl"];
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
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    string deploymentUrl = null;
                    string projectName = null;

                    // Extract project name
                    if (root.TryGetProperty("projectName", out var projNameProp))
                    {
                        projectName = projNameProp.GetString();
                    }

                    // Try to get deploymentUrl directly
                    if (root.TryGetProperty("deploymentUrl", out var deployProp))
                    {
                        deploymentUrl = deployProp.GetString();
                    }
                    // Try nested deploymentInfo.url
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

                        // Ensure it ends with /
                        if (!deploymentUrl.EndsWith("/"))
                        {
                            deploymentUrl += "/";
                        }
                    }

                    _logger.LogInformation("✅ Vercel deployment URL: {Url}", deploymentUrl);

                    return new DeploymentResponse
                    {
                        Success = true,
                        Message = "Vercel deployment initiated successfully",
                        DeploymentUrl = deploymentUrl,
                        GitHubRepoUrl = $"https://github.com/{repoPath}",
                        ProjectId = root.TryGetProperty("projectId", out var pid) ? pid.GetString() : null
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

                // Split repo path
                var repoParts = repoPath.Split('/');
                if (repoParts.Length < 2)
                    return new DeploymentResponse { Success = false, Message = "Repository path must be in format 'owner/repo'" };

                var owner = repoParts[0];
                var repoName = repoParts[1];

                _logger.LogInformation("Deploying {Repo} to GitHub Pages on branch {Branch}", repoPath, branch);

                // Call internal GitHub Pages controller
                var baseUrl = _configuration["BaseUrl"];
                var enableUrl = $"{baseUrl}/api/githubpages/enable";

                using var client = new HttpClient();

                // Add GitHub token
                if (!string.IsNullOrWhiteSpace(githubToken))
                    client.DefaultRequestHeaders.Add("GitHub-Token", githubToken);
                else
                    return new DeploymentResponse { Success = false, Message = "GitHub token is required" };

                // Build request payload
                var requestData = new
                {
                    Owner = owner,
                    Repo = repoName,
                    Branch = branch,
                    Path = config?.OutputDirectory ?? "/",  // "/" or "/docs"
                    BuildType = config?.Framework ?? "legacy",  // "legacy" or "workflow"
                    TriggerBuild = true  // Trigger immediate deployment
                };

                var json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Sending to GitHub Pages controller:\n{Payload}", json);

                var resp = await client.PostAsync(enableUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                var body = await resp.Content.ReadAsStringAsync();

                _logger.LogInformation("GitHub Pages controller response ({Status}):\n{Body}", resp.StatusCode, body);

                if (!resp.IsSuccessStatusCode)
                {
                    // Extract error message
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
                // Format: https://{owner}.github.io/{repo}/
                var deploymentUrl = $"https://{owner}.github.io/{repoName}/";

                _logger.LogInformation("✅ GitHub Pages deployment URL: {Url}", deploymentUrl);

                return new DeploymentResponse
                {
                    Success = true,
                    Message = "GitHub Pages deployment initiated successfully",
                    DeploymentUrl = deploymentUrl,
                    GitHubRepoUrl = $"https://github.com/{repoPath}",
                    ProjectId = null
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

        public async Task<string> UploadAndExtractProjectAsync(string zipFile, string projectName)
        {
            // If zipFile is actually a blob name → reuse existing logic
            return await DownloadAndExtractFromAzureAsync(zipFile, projectName);
        }
        public async Task<DeploymentResponse> ExecuteUploadDeployment(
            UploadProjectRequest request,
            string userId)
        {
            try
            {
                _logger.LogInformation("Starting upload deployment for {Project}", request.ProjectName);

                // ⭐ STEP 1: Extract ZIP
                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                var zipPath = Path.Combine(tempPath, request.ZipPath);

                using (var stream = new FileStream(zipPath, FileMode.Create))
                {
                    await request.ProjectZip.CopyToAsync(stream);
                }

                var extractPath = Path.Combine(tempPath, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                var actualProjectPath = GetActualProjectRoot(extractPath);

                // ⭐ STEP 2: Create repo
                var repoName = await CreateSwiftDeployRepoAsync(request.ProjectName, request.Description);

                // ⭐ STEP 3: Push code
                var pushed = await PushCodeToSwiftDeployRepoAsync(repoName, actualProjectPath);

                if (!pushed)
                    throw new Exception("Failed to push code");

                // ⭐ STEP 4: Push config
                await PushConfigToRepoAsync(repoName, request.Platform, request.Config);

                // ⭐ STEP 5: Get tokens
                var githubToken = await GetUserGitHubTokenAsync(userId);
                var platformToken = await GetPlatformTokenAsync(userId, request.Platform);

                if (string.IsNullOrEmpty(githubToken))
                    throw new Exception("GitHub token missing");

                // ⭐ STEP 6: Deploy
                return request.Platform.ToLower() switch
                {
                    "vercel" => await DeployToVercelWithUserRepo(
                        $"{(await _gitHubClient.User.Current()).Login}/{repoName}",
                        "main",
                        request.Config,
                        platformToken,
                        githubToken
                    ),

                    "netlify" => await DeployToNetlifyWithUserRepo(
                        $"{(await _gitHubClient.User.Current()).Login}/{repoName}",
                        "main",
                        request.Config,
                        platformToken,
                        githubToken
                    ),

                    "cloudflare" => await DeployToCloudflareWithUserRepo(
                        $"{(await _gitHubClient.User.Current()).Login}/{repoName}",
                        "main",
                        request.Config,
                        platformToken,
                        githubToken
                    ),

                    _ => throw new Exception("Unsupported platform")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload deployment failed");

                return new DeploymentResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }


        public async Task<DeploymentResponse> ExecuteGitHubDeployment(GitHubDeployRequest request)
        {
            try
            {
                _logger.LogInformation("Starting GitHub deployment for {Repo}", request.GitHubRepo);

                // ⭐ Extract owner/repo
                var parts = request.GitHubRepo.Split('/');
                if (parts.Length != 2)
                    throw new Exception("Invalid GitHub repo format. Expected owner/repo");

                var owner = parts[0];
                var repoName = parts[1];
                var branch = string.IsNullOrEmpty(request.Branch) ? "main" : request.Branch;

                // ⭐ Get tokens (IMPORTANT for scheduler)
                var githubToken = await GetUserGitHubTokenAsync(request.UserId);
                var platformToken = await GetPlatformTokenAsync(request.UserId, request.Platform);

                if (string.IsNullOrEmpty(githubToken))
                    throw new Exception("GitHub token not found");

                if (string.IsNullOrEmpty(platformToken))
                    throw new Exception($"{request.Platform} token not found");

                var repoPath = $"{owner}/{repoName}";

                // ⭐ Use SAME flow as your working APIs
                return request.Platform.ToLower() switch
                {
                    "vercel" => await DeployToVercelWithUserRepo(
                        repoPath,
                        branch,
                        request.Config,
                        platformToken,
                        githubToken
                    ),

                    "netlify" => await DeployToNetlifyWithUserRepo(
                        repoPath,
                        branch,
                        request.Config,
                        platformToken,
                        githubToken
                    ),

                    "cloudflare" => await DeployToCloudflareWithUserRepo(
                        repoPath,
                        branch,
                        request.Config,
                        platformToken,
                        githubToken
                    ),

                    _ => throw new Exception("Unsupported platform")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GitHub deployment failed");

                return new DeploymentResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
    }
}