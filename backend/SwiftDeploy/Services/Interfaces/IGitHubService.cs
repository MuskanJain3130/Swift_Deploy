// Services/IGitHubService.cs

// Services/IGitHubService.cs
using SwiftDeploy.Models;

namespace SwiftDeploy.Services.Interfaces
{
    public interface IGitHubService
    {
        Task<GitHubSaveResult> SaveFileToRepoAsync(string repo, string filePath, string content, string commitMessage, string branch = "main", string accessToken = null);
    }
}