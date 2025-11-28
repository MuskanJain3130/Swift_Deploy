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

    // Models/ConfigModels.cs
    public class CommonConfig
    {
        [Required]
        public string ProjectName { get; set; }
        public string? BuildCommand { get; set; }
        public string OutputDirectory { get; set; } = ".";
        public string? InstallCommand { get; set; }
        public string? NodeVersion { get; set; }
        public string? Domain { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public List<RedirectRule> Redirects { get; set; } = new();
        public List<HeaderRule> Headers { get; set; } = new();
        public string TeamId { get; set; } = "";// Default to personal account

        public bool EnableHttps { get; set; } = true;

        // Update Framework to support static
        public string Framework { get; set; } = "static"; // Default to static

        // Add project type
        public ProjectType ProjectType { get; set; } = ProjectType.Static;
    }
    public enum ProjectType
    {
        Static,      // Plain HTML/CSS/JS
        Framework    // React, Vue, Next.js, etc.
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