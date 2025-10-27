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
                    throw new UnauthorizedAccessException("GitHub access token is required");
                }

                var client = new GitHubClient(new Octokit.ProductHeaderValue("SwiftDeploy"));
                client.Credentials = new Credentials(token);

                var repoParts = repo.Split('/');
                if (repoParts.Length != 2)
                {
                    throw new ArgumentException("Repository must be in format 'owner/repo'");
                }

                var owner = repoParts[0];
                var repoName = repoParts[1];

                RepositoryContent existingFile = null;
                try
                {
                    var files = await client.Repository.Content.GetAllContents(owner, repoName, filePath);
                    existingFile = files.FirstOrDefault();
                }
                catch (NotFoundException)
                {
                    _logger.LogInformation($"File {filePath} doesn't exist, will create new file");
                }

                if (existingFile != null)
                {
                    var updateRequest = new UpdateFileRequest(commitMessage, content, existingFile.Sha, branch);
                    var updateResult = await client.Repository.Content.UpdateFile(owner, repoName, filePath, updateRequest);

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
                    var createRequest = new CreateFileRequest(commitMessage, content, branch);
                    var createResult = await client.Repository.Content.CreateFile(owner, repoName, filePath, createRequest);

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
                _logger.LogError(ex, $"GitHub API error while saving file {filePath} to {repo}");
                return new GitHubSaveResult
                {
                    Success = false,
                    Message = $"GitHub API error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving file {filePath} to GitHub repo {repo}");
                return new GitHubSaveResult
                {
                    Success = false,
                    Message = $"Error saving file to GitHub: {ex.Message}"
                };
            }
        }
    }
}