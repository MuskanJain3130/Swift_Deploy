// Services/GitHubService.cs
using Microsoft.Extensions.Configuration;
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Services.Interfaces;
using System.Linq;

namespace SwiftDeploy.Services
{
    public class GitHubService : IGitHubService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GitHubService> _logger;
        private readonly IServiceProvider _serviceProvider; // Add this


        public GitHubService(IConfiguration configuration, ILogger<GitHubService> logger, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<GitHubSaveResult> SaveFileToRepoAsync(
            string repo,
            string filePath,
            string content,
            string commitMessage,
            string branch = "main",
            string accessToken = null)
        {
            try
            {
                var token = accessToken ?? _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("GitHub access token is missing");
                    throw new UnauthorizedAccessException("GitHub access token is required");
                }

                var client = new GitHubClient(new Octokit.ProductHeaderValue("SwiftDeploy"));
                client.Credentials = new Credentials(token);

                var repoParts = repo.Split('/');
                if (repoParts.Length != 2)
                {
                    _logger.LogError($"Invalid repository format: {repo}");
                    throw new ArgumentException("Repository must be in format 'owner/repo'");
                }

                var owner = repoParts[0];
                var repoName = repoParts[1];

                _logger.LogInformation($"Attempting to save file to: {owner}/{repoName}/{filePath} on branch: {branch}");

                // Step 1: Verify repository exists
                Octokit.Repository repository; // *** FIX: Use fully qualified name ***
                try
                {
                    repository = await client.Repository.Get(owner, repoName);
                    _logger.LogInformation($"Repository found: {repository.FullName}, Default Branch: {repository.DefaultBranch}");
                }
                catch (NotFoundException)
                {
                    _logger.LogError($"Repository not found: {owner}/{repoName}");
                    return new GitHubSaveResult
                    {
                        Success = false,
                        Message = $"Repository '{owner}/{repoName}' not found or no access."
                    };
                }

                // Step 2: Verify branch exists
                try
                {
                    var branchRef = await client.Git.Reference.Get(owner, repoName, $"heads/{branch}");
                    _logger.LogInformation($"Branch '{branch}' exists. SHA: {branchRef.Object.Sha}");
                }
                catch (NotFoundException)
                {
                    _logger.LogError($"Branch '{branch}' not found in {owner}/{repoName}");
                    return new GitHubSaveResult
                    {
                        Success = false,
                        Message = $"Branch '{branch}' does not exist in repository. Available branch might be '{repository.DefaultBranch}'."
                    };
                }

                // Step 3: Check if file exists on the branch
                RepositoryContent existingFile = null;
                try
                {
                    var files = await client.Repository.Content.GetAllContentsByRef(owner, repoName, filePath, branch);
                    existingFile = files.FirstOrDefault();
                    _logger.LogInformation($"File exists on branch '{branch}': {filePath}, SHA: {existingFile?.Sha}");
                }
                catch (NotFoundException)
                {
                    _logger.LogInformation($"File doesn't exist on branch '{branch}', will create new file: {filePath}");
                }

                // Step 4: Create or update file
                try
                {
                    if (existingFile != null)
                    {
                        // Update existing file
                        _logger.LogInformation($"Updating existing file: {filePath} on branch: {branch}");

                        var updateRequest = new UpdateFileRequest(commitMessage, content, existingFile.Sha)
                        {
                            Branch = branch
                        };

                        var updateResult = await client.Repository.Content.UpdateFile(owner, repoName, filePath, updateRequest);

                        _logger.LogInformation($"File updated successfully. Commit SHA: {updateResult.Commit.Sha}");

                        return new GitHubSaveResult
                        {
                            Success = true,
                            Message = "File updated successfully",
                            CommitSha = updateResult.Commit.Sha,
                            FileUrl = updateResult.Content.HtmlUrl,
                            Branch = branch
                        };
                    }
                    else
                    {
                        // Create new file
                        _logger.LogInformation($"Creating new file: {filePath} on branch: {branch}");

                        var createRequest = new CreateFileRequest(commitMessage, content)
                        {
                            Branch = branch
                        };

                        var createResult = await client.Repository.Content.CreateFile(owner, repoName, filePath, createRequest);

                        _logger.LogInformation($"File created successfully. Commit SHA: {createResult.Commit.Sha}");

                        return new GitHubSaveResult
                        {
                            Success = true,
                            Message = "File created successfully",
                            CommitSha = createResult.Commit.Sha,
                            FileUrl = createResult.Content.HtmlUrl,
                            Branch = branch
                        };
                    }
                }
                catch (ApiException apiEx)
                {
                    _logger.LogError(apiEx, $"GitHub API error during file operation: {apiEx.StatusCode}");
                    _logger.LogError($"API Response: {apiEx.HttpResponse?.Body}");

                    return new GitHubSaveResult
                    {
                        Success = false,
                        Message = $"GitHub API error: {apiEx.Message} (Status: {apiEx.StatusCode}). Check token permissions (needs 'repo' scope)."
                    };
                }
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, $"GitHub API error: StatusCode={ex.StatusCode}, Message={ex.Message}");

                return new GitHubSaveResult
                {
                    Success = false,
                    Message = $"GitHub API error: {ex.Message} (Status: {ex.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error saving file {filePath} to GitHub repo {repo}");
                return new GitHubSaveResult
                {
                    Success = false,
                    Message = $"Error saving file to GitHub: {ex.Message}"
                };
            }
        }

        public async Task<GitHubSaveResult> GenerateAndSaveConfigAsync(
            string platform,
            string gitHubRepo,
            string gitHubToken,
            string branch,
            CommonConfig config)
        {
            try
            {
                // Generate config
                var templateEngine = _serviceProvider.GetRequiredService<ITemplateEngine>();
                var configContent = await templateEngine.GenerateConfigAsync(platform, config);
                var fileName = templateEngine.GetConfigFileName(platform);

                // Save to GitHub
                return await SaveFileToRepoAsync(
                    gitHubRepo,
                    fileName,
                    configContent,
                    $"Add {platform} configuration via SwiftDeploy",
                    branch,
                    gitHubToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating and saving config for {platform}");
                return new GitHubSaveResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
    }
}