// Services/IUnifiedDeploymentService.cs
using Octokit;
using SwiftDeploy.Models;
using DeploymentStatus = SwiftDeploy.Models.DeploymentStatus;

        public interface IUnifiedDeploymentService
        {
            Task<string> UploadAndExtractProjectAsync(IFormFile zipFile, string projectName);
            Task<string> CreateSwiftDeployRepoAsync(string projectName, string description = null);
            Task<bool> PushCodeToSwiftDeployRepoAsync(string repoName, string localProjectPath);
            Task<bool> PushConfigToRepoAsync(string repoName, string platform, CommonConfig config);

            // Update these to accept userId and platformToken
            Task<DeploymentResponse> DeployToCloudflareAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken);
            Task<DeploymentResponse> DeployToNetlifyAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken);
            Task<DeploymentResponse> DeployToVercelAsync(string repoName, string branch, CommonConfig config, string userId, string platformToken);

            Task<ProjectInfo> GetProjectInfoAsync(string projectId);
            Task UpdateProjectStatusAsync(string projectId, DeploymentStatus status, string message = null);
        }
    