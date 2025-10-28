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

        public GitHubService(IConfiguration configuration, ILogger<GitHubService> logger)
        {
            _configuration = configuration;
            _logger = logger;
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

                // Step 1: Verify repository exists and we have access
                try
                {
                    var repository = await client.Repository.Get(owner, repoName);
                    _logger.LogInformation($"Repository found: {repository.FullName}, Default Branch: {repository.DefaultBranch}");
                }
                catch (NotFoundException)
                {
                    _logger.LogError($"Repository not found: {owner}/{repoName}");
                    return new GitHubSaveResult
                    {
                        Success = false,
                        Message = $"Repository '{owner}/{repoName}' not found. Please check the repository name and ensure your token has access."
                    };
                }
                catch (ApiException apiEx)
                {
                    _logger.LogError(apiEx, $"GitHub API error accessing repository: {apiEx.StatusCode}");
                    return new GitHubSaveResult
                    {
                        Success = false,
                        Message = $"Cannot access repository: {apiEx.Message}"
                    };
                }

                // Step 2: Check if file already exists
                RepositoryContent existingFile = null;
                try
                {
                    var files = await client.Repository.Content.GetAllContents(owner, repoName, filePath);
                    existingFile = files.FirstOrDefault();
                    _logger.LogInformation($"File exists: {filePath}, SHA: {existingFile?.Sha}");
                }
                catch (NotFoundException)
                {
                    _logger.LogInformation($"File doesn't exist, will create new file: {filePath}");
                }

                // Step 3: Create or update file
                if (existingFile != null)
                {
                    // Update existing file
                    _logger.LogInformation($"Updating existing file: {filePath}");
                    var updateRequest = new UpdateFileRequest(commitMessage, content, existingFile.Sha, branch);
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
                    _logger.LogInformation($"Creating new file: {filePath}");
                    var createRequest = new CreateFileRequest(commitMessage, content, branch);
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
    }
}