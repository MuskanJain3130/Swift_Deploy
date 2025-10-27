using System.ComponentModel.DataAnnotations;

namespace SwiftDeploy.Models
{
        public class GitHubCommitResponse
        {
            public GitHubCommitInfo Commit { get; set; }
            public GitHubFileContent Content { get; set; }
        }

        public class GitHubCommitInfo
        {
            public string Sha { get; set; }
            public string Message { get; set; }
        }
        public class GitHubTokenResponse
        {
            public string AccessToken { get; set; }
            public string TokenType { get; set; }
            public string Scope { get; set; }
        }

        public class GitHubUser
        {
            public long Id { get; set; }
            public string Login { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string AvatarUrl { get; set; }
        }

        public class GitHubEmail
        {
            public string Email { get; set; }
            public bool Primary { get; set; }
            public bool Verified { get; set; }
            public string Visibility { get; set; }
        }
    public class GitHubSaveResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string CommitSha { get; set; }
            public string FileUrl { get; set; }
            public string Branch { get; set; }
        }

        public class GitHubFileContent
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Sha { get; set; }
            public string Content { get; set; }
            public string Encoding { get; set; }
            public string HtmlUrl { get; set; }
    }// In your GitHubModels.cs file, update the GitHubDeployRequest class:
    public class GitHubDeployRequest
    {
        [Required]
        public string UserId { get; set; } // Add this line

        [Required]
        public string ProjectName { get; set; }
        public string Description { get; set; }
        [Required]
        public string Platform { get; set; }
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$",
            ErrorMessage = "GitHub repository must be in format 'owner/repo'")]
        public string GitHubRepo { get; set; }
        [Required]
        public string GitHubToken { get; set; }
        public string Branch { get; set; } = "main";
        [Required]
        public CommonConfig Config { get; set; }
    }
    public enum DeploymentStatus
        {
            Uploading,
            Processing,
            CreatingRepo,
            PushingCode,
            GeneratingConfig,
            Deploying,
            Completed,
            Failed
        }

        public class DeploymentResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string ProjectId { get; set; }
            public string GitHubRepoUrl { get; set; }
            public string DeploymentUrl { get; set; }
            public string ConfigFileUrl { get; set; }
            public DeploymentStatus Status { get; set; }
        }

        public class DeploymentStatusResponse
        {
            public string ProjectId { get; set; }
            public DeploymentStatus Status { get; set; }
            public string Message { get; set; }
            public int Progress { get; set; }
            public string CurrentStep { get; set; }
            public string DeploymentUrl { get; set; }
            public string GitHubRepoUrl { get; set; }
        }

        public class ProjectInfo
        {
            public string ProjectId { get; set; }
            public string ProjectName { get; set; }
            public string Description { get; set; }
            public string Platform { get; set; }
            public DateTime CreatedAt { get; set; }
            public DeploymentStatus Status { get; set; }
            public CommonConfig Config { get; set; }
            public string GitHubRepoName { get; set; }
            public string GitHubRepoUrl { get; set; }
            public string DeploymentUrl { get; set; }
        }
}
