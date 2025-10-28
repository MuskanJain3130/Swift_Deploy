// Services/TemplateEngine.cs
using SwiftDeploy.Models;
using System.Text.Json;
using System.Text;
using SwiftDeploy.Services.Interfaces;

namespace SwiftDeploy.Services
{
    public class TemplateEngine : ITemplateEngine
    {
        public async Task<string> GenerateConfigAsync(string platform, CommonConfig config)
        {
            return platform.ToLower() switch
            {
                "vercel" => await GenerateVercelConfigAsync(config),
                "cloudflare" => await GenerateCloudflareConfigAsync(config),
                "githubpages" => await GenerateGitHubPagesConfigAsync(config),
                "netlify" => await GenerateNetlifyConfigAsync(config),
                _ => throw new ArgumentException($"Unsupported platform: {platform}")
            };
        }

        public string GetConfigFileName(string platform)
        {
            return platform.ToLower() switch
            {
                "vercel" => "vercel.json",
                "cloudflare" => "wrangler.toml",
                "githubpages" => ".github/workflows/deploy.yml",
                "netlify" => "netlify.toml",
                _ => throw new ArgumentException($"Unsupported platform: {platform}")
            };
        }
        public string GetContentType(string platform)
        {
            return platform.ToLower() switch
            {
                "vercel" => "application/json",
                "cloudflare" => "text/plain",
                "githubpages" => "text/yaml",
                "netlify" => "text/plain",
                _ => "text/plain"
            };
        }

        private async Task<string> GenerateVercelConfigAsync(CommonConfig config)
        {
            var vercelConfig = new Dictionary<string, object>
            {
                ["version"] = 2,
                ["name"] = config.ProjectName
            };

            // For static projects
            if (config.ProjectType == ProjectType.Static)
            {
                vercelConfig["builds"] = new[]
                {
            new
            {
                src = config.OutputDirectory == "." ? "**/*" : $"{config.OutputDirectory}/**",
                use = "@vercel/static"
            }
        };
            }
            else
            {
                // For framework projects
                vercelConfig["buildCommand"] = config.BuildCommand;
                vercelConfig["outputDirectory"] = config.OutputDirectory;
                vercelConfig["installCommand"] = config.InstallCommand;
                vercelConfig["devCommand"] = "npm run dev";
                vercelConfig["framework"] = config.Framework;
            }

            // Common configurations
            if (config.EnvironmentVariables?.Any() == true)
            {
                vercelConfig["env"] = config.EnvironmentVariables;
            }

            if (config.Redirects?.Any() == true)
            {
                vercelConfig["redirects"] = config.Redirects.Select(r => new
                {
                    source = r.From,
                    destination = r.To,
                    statusCode = r.Status
                }).ToArray();
            }

            if (config.Headers?.Any() == true)
            {
                vercelConfig["headers"] = config.Headers.Select(h => new
                {
                    source = h.Source,
                    headers = h.Headers.Select(kv => new
                    {
                        key = kv.Key,
                        value = kv.Value
                    }).ToArray()
                }).ToArray();
            }

            return JsonSerializer.Serialize(vercelConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        private async Task<string> GenerateCloudflareConfigAsync(CommonConfig config)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"name = \"{config.ProjectName}\"");
            sb.AppendLine("compatibility_date = \"2023-05-18\"");
            sb.AppendLine();

            // Build configuration
            sb.AppendLine("[build]");

            if (config.ProjectType == ProjectType.Static)
            {
                // For static projects, no build command needed
                sb.AppendLine($"command = \"\"");
                sb.AppendLine($"publish = \"{config.OutputDirectory}\"");
            }
            else
            {
                // For framework projects
                sb.AppendLine($"command = \"{config.BuildCommand}\"");
                sb.AppendLine($"publish = \"{config.OutputDirectory}\"");
            }
            sb.AppendLine();

            // Environment variables
            if (config.EnvironmentVariables?.Any() == true)
            {
                sb.AppendLine("[vars]");
                foreach (var env in config.EnvironmentVariables)
                {
                    sb.AppendLine($"{env.Key} = \"{env.Value}\"");
                }
                sb.AppendLine();
            }

            // Redirects
            if (config.Redirects?.Any() == true)
            {
                foreach (var redirect in config.Redirects)
                {
                    sb.AppendLine("[[redirects]]");
                    sb.AppendLine($"from = \"{redirect.From}\"");
                    sb.AppendLine($"to = \"{redirect.To}\"");
                    sb.AppendLine($"status = {redirect.Status}");
                    sb.AppendLine();
                }
            }

            // Headers
            if (config.Headers?.Any() == true)
            {
                foreach (var header in config.Headers)
                {
                    sb.AppendLine("[[headers]]");
                    sb.AppendLine($"for = \"{header.Source}\"");
                    sb.AppendLine("[headers.values]");
                    foreach (var h in header.Headers)
                    {
                        sb.AppendLine($"{h.Key} = \"{h.Value}\"");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        private async Task<string> GenerateGitHubPagesConfigAsync(CommonConfig config)
        {
            var sb = new StringBuilder();

            sb.AppendLine("name: Deploy to GitHub Pages");
            sb.AppendLine();
            sb.AppendLine("on:");
            sb.AppendLine("  push:");
            sb.AppendLine("    branches: [ main ]");
            sb.AppendLine("  pull_request:");
            sb.AppendLine("    branches: [ main ]");
            sb.AppendLine();
            sb.AppendLine("jobs:");
            sb.AppendLine("  build-and-deploy:");
            sb.AppendLine("    runs-on: ubuntu-latest");
            sb.AppendLine();
            sb.AppendLine("    steps:");
            sb.AppendLine("    - name: Checkout");
            sb.AppendLine("      uses: actions/checkout@v3");
            sb.AppendLine();

            if (config.ProjectType == ProjectType.Static)
            {
                // For static projects, skip Node.js setup and build steps
                sb.AppendLine("    - name: Deploy to GitHub Pages");
                sb.AppendLine("      uses: peaceiris/actions-gh-pages@v3");
                sb.AppendLine("      with:");
                sb.AppendLine("        github_token: ${{ secrets.GITHUB_TOKEN }}");
                sb.AppendLine($"        publish_dir: ./{config.OutputDirectory}");
            }
            else
            {
                // For framework projects, include full build process
                sb.AppendLine("    - name: Setup Node.js");
                sb.AppendLine("      uses: actions/setup-node@v3");
                sb.AppendLine("      with:");
                sb.AppendLine($"        node-version: '{config.NodeVersion ?? "18"}'");
                sb.AppendLine("        cache: 'npm'");
                sb.AppendLine();
                sb.AppendLine("    - name: Install dependencies");
                sb.AppendLine($"      run: {config.InstallCommand ?? "npm install"}");
                sb.AppendLine();
                sb.AppendLine("    - name: Build");
                sb.AppendLine($"      run: {config.BuildCommand}");

                if (config.EnvironmentVariables?.Any() == true)
                {
                    sb.AppendLine("      env:");
                    foreach (var env in config.EnvironmentVariables)
                    {
                        sb.AppendLine($"        {env.Key}: ${{{{ secrets.{env.Key} }}}}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("    - name: Deploy to GitHub Pages");
                sb.AppendLine("      uses: peaceiris/actions-gh-pages@v3");
                sb.AppendLine("      with:");
                sb.AppendLine("        github_token: ${{ secrets.GITHUB_TOKEN }}");
                sb.AppendLine($"        publish_dir: ./{config.OutputDirectory}");
            }

            if (!string.IsNullOrEmpty(config.Domain))
            {
                sb.AppendLine($"        cname: {config.Domain}");
            }

            return sb.ToString();
        }
        private async Task<string> GenerateNetlifyConfigAsync(CommonConfig config)
        {
            var sb = new StringBuilder();

            sb.AppendLine("[build]");

            if (config.ProjectType == ProjectType.Static)
            {
                // For static projects, no build command needed
                sb.AppendLine($"  publish = \"{config.OutputDirectory}\"");
            }
            else
            {
                // For framework projects
                sb.AppendLine($"  command = \"{config.BuildCommand}\"");
                sb.AppendLine($"  publish = \"{config.OutputDirectory}\"");
            }
            sb.AppendLine();

            // Environment variables
            if (config.EnvironmentVariables?.Any() == true)
            {
                sb.AppendLine("[build.environment]");
                foreach (var env in config.EnvironmentVariables)
                {
                    sb.AppendLine($"  {env.Key} = \"{env.Value}\"");
                }
                sb.AppendLine();
            }

            // Redirects
            if (config.Redirects?.Any() == true)
            {
                foreach (var redirect in config.Redirects)
                {
                    sb.AppendLine("[[redirects]]");
                    sb.AppendLine($"  from = \"{redirect.From}\"");
                    sb.AppendLine($"  to = \"{redirect.To}\"");
                    sb.AppendLine($"  status = {redirect.Status}");
                    sb.AppendLine();
                }
            }

            // Headers
            if (config.Headers?.Any() == true)
            {
                foreach (var header in config.Headers)
                {
                    sb.AppendLine("[[headers]]");
                    sb.AppendLine($"  for = \"{header.Source}\"");
                    sb.AppendLine("  [headers.values]");
                    foreach (var h in header.Headers)
                    {
                        sb.AppendLine($"    {h.Key} = \"{h.Value}\"");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }
}