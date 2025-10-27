// Models/ConfigModels.cs
using System.ComponentModel.DataAnnotations;

namespace SwiftDeploy.Models
{
    public class ConfigRequest
    {
        [Required]
        public string Platform { get; set; } // vercel, cloudflare, githubpages, netlify

        [Required]
        public CommonConfig Config { get; set; }
    }

    public class GitHubConfigRequest : ConfigRequest
    {
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$",
            ErrorMessage = "GitHub repository must be in format 'owner/repo'")]
        public string GitHubRepo { get; set; }

        public string Branch { get; set; } = "main";
        public string CommitMessage { get; set; }

        [Required]
        public string GitHubToken { get; set; }
    }

    public class CommonConfig
    {
        public string ProjectName { get; set; }
        public string BuildCommand { get; set; } = "npm run build";
        public string OutputDirectory { get; set; } = "dist";
        public string InstallCommand { get; set; } = "npm install";
        public string NodeVersion { get; set; } = "18.x";
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public List<RedirectRule> Redirects { get; set; } = new();
        public List<HeaderRule> Headers { get; set; } = new();
        public string Domain { get; set; }
        public bool EnableHttps { get; set; } = true;
        public string Framework { get; set; }
    }

    public class RedirectRule
    {
        public string From { get; set; }
        public string To { get; set; }
        public int Status { get; set; } = 301;
    }

    public class HeaderRule
    {
        public string Source { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public class ConfigResponse
    {
        public string Platform { get; set; }
        public string FileName { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
    }

    public class GitHubSaveResponse
    {
        public string Platform { get; set; }
        public string FileName { get; set; }
        public string GitHubRepo { get; set; }
        public string Branch { get; set; }
        public string CommitSha { get; set; }
        public string FileUrl { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}