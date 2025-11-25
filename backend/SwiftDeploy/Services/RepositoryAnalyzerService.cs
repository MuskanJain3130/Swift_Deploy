using Octokit;
using SwiftDeploy.Models;
using System.Text.Json;

namespace SwiftDeploy.Services
{
    public class RepositoryAnalyzerService
    {
        private readonly GitHubClient _githubClient;

        public RepositoryAnalyzerService(GitHubClient githubClient)
        {
            _githubClient = githubClient;
        }

        public async Task<RepositoryAnalysis> AnalyzeRepository(string owner, string repoName, string branch = "main")
        {
            var analysis = new RepositoryAnalysis
            {
                Owner = owner,
                RepoName = repoName,
                DetectedTechnologies = new List<string>(),
                Suggestions = new List<PlatformSuggestion>()
            };

            try
            {
                // Get root contents
                var rootContents = await _githubClient.Repository.Content.GetAllContents(owner, repoName);

                // Detect package.json and analyze
                var packageJson = rootContents.FirstOrDefault(c => c.Name.Equals("package.json", StringComparison.OrdinalIgnoreCase));
                if (packageJson != null)
                {
                    await AnalyzePackageJson(owner, repoName, analysis);
                }

                // Detect configuration files
                await DetectConfigurationFiles(rootContents, analysis);

                // Detect static files
                DetectStaticSite(rootContents, analysis);

                // Detect framework-specific files
                await DetectFrameworkSpecificFiles(owner, repoName, rootContents, analysis);

                // Generate platform suggestions
                GeneratePlatformSuggestions(analysis);

                return analysis;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error analyzing repository: {ex.Message}", ex);
            }
        }

        private async Task AnalyzePackageJson(string owner, string repoName, RepositoryAnalysis analysis)
        {
            try
            {
                var packageJsonContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "package.json");
                var packageJsonString = System.Text.Encoding.UTF8.GetString(packageJsonContent);
                var packageJson = JsonSerializer.Deserialize<JsonElement>(packageJsonString);

                // Detect package manager
                var rootContents = await _githubClient.Repository.Content.GetAllContents(owner, repoName);
                if (rootContents.Any(c => c.Name == "pnpm-lock.yaml"))
                    analysis.PackageManager = "pnpm";
                else if (rootContents.Any(c => c.Name == "yarn.lock"))
                    analysis.PackageManager = "yarn";
                else if (rootContents.Any(c => c.Name == "package-lock.json"))
                    analysis.PackageManager = "npm";
                else
                    analysis.PackageManager = "npm";

                // Analyze dependencies
                if (packageJson.TryGetProperty("dependencies", out var deps))
                {
                    AnalyzeDependencies(deps, analysis);
                }

                if (packageJson.TryGetProperty("devDependencies", out var devDeps))
                {
                    AnalyzeDependencies(devDeps, analysis);
                }

                // Detect scripts
                if (packageJson.TryGetProperty("scripts", out var scripts))
                {
                    AnalyzeScripts(scripts, analysis);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing package.json: {ex.Message}");
            }
        }

        private void AnalyzeDependencies(JsonElement dependencies, RepositoryAnalysis analysis)
        {
            var depNames = dependencies.EnumerateObject().Select(p => p.Name.ToLower()).ToList();

            // Framework detection
            if (depNames.Contains("next"))
            {
                analysis.Framework = "Next.js";
                analysis.DetectedTechnologies.Add("Next.js");
                analysis.HasServerSideRendering = true;
                analysis.HasApiRoutes = true;
            }
            else if (depNames.Contains("nuxt"))
            {
                analysis.Framework = "Nuxt.js";
                analysis.DetectedTechnologies.Add("Nuxt.js");
                analysis.HasServerSideRendering = true;
            }
            else if (depNames.Contains("gatsby"))
            {
                analysis.Framework = "Gatsby";
                analysis.DetectedTechnologies.Add("Gatsby");
                analysis.IsStatic = true;
            }
            else if (depNames.Contains("react"))
            {
                analysis.Framework = "React";
                analysis.DetectedTechnologies.Add("React");
            }
            else if (depNames.Contains("vue"))
            {
                analysis.Framework = "Vue.js";
                analysis.DetectedTechnologies.Add("Vue.js");
            }
            else if (depNames.Contains("svelte"))
            {
                analysis.Framework = "Svelte";
                analysis.DetectedTechnologies.Add("Svelte");
            }
            else if (depNames.Contains("astro"))
            {
                analysis.Framework = "Astro";
                analysis.DetectedTechnologies.Add("Astro");
                analysis.IsStatic = true;
            }
            else if (depNames.Contains("angular"))
            {
                analysis.Framework = "Angular";
                analysis.DetectedTechnologies.Add("Angular");
            }

            // Build tool detection
            if (depNames.Contains("vite"))
            {
                analysis.BuildTool = "Vite";
                analysis.DetectedTechnologies.Add("Vite");
            }
            else if (depNames.Contains("webpack"))
            {
                analysis.BuildTool = "Webpack";
                analysis.DetectedTechnologies.Add("Webpack");
            }
            else if (depNames.Contains("parcel"))
            {
                analysis.BuildTool = "Parcel";
                analysis.DetectedTechnologies.Add("Parcel");
            }

            // Edge functions detection
            if (depNames.Contains("@vercel/edge") || depNames.Contains("@cloudflare/workers-types"))
            {
                analysis.HasEdgeFunctions = true;
                analysis.DetectedTechnologies.Add("Edge Functions");
            }

            // Additional technologies
            if (depNames.Contains("typescript"))
                analysis.DetectedTechnologies.Add("TypeScript");
            if (depNames.Contains("tailwindcss"))
                analysis.DetectedTechnologies.Add("Tailwind CSS");
        }

        private void AnalyzeScripts(JsonElement scripts, RepositoryAnalysis analysis)
        {
            var scriptNames = scripts.EnumerateObject().Select(p => p.Name.ToLower()).ToList();
            var scriptValues = scripts.EnumerateObject().Select(p => p.Value.GetString()?.ToLower() ?? "").ToList();

            // Detect build commands
            if (scriptValues.Any(s => s.Contains("next build")))
            {
                analysis.Framework = analysis.Framework ?? "Next.js";
            }
            else if (scriptValues.Any(s => s.Contains("nuxt build")))
            {
                analysis.Framework = analysis.Framework ?? "Nuxt.js";
            }
            else if (scriptValues.Any(s => s.Contains("gatsby build")))
            {
                analysis.Framework = analysis.Framework ?? "Gatsby";
                analysis.IsStatic = true;
            }
            else if (scriptValues.Any(s => s.Contains("vite build")))
            {
                analysis.BuildTool = "Vite";
            }
        }

        private async Task DetectConfigurationFiles(IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            // Next.js
            if (fileNames.Contains("next.config.js") || fileNames.Contains("next.config.mjs"))
            {
                analysis.Framework = analysis.Framework ?? "Next.js";
                analysis.DetectedTechnologies.Add("Next.js Config");
            }

            // Nuxt
            if (fileNames.Contains("nuxt.config.js") || fileNames.Contains("nuxt.config.ts"))
            {
                analysis.Framework = analysis.Framework ?? "Nuxt.js";
                analysis.DetectedTechnologies.Add("Nuxt Config");
            }

            // Vite
            if (fileNames.Contains("vite.config.js") || fileNames.Contains("vite.config.ts"))
            {
                analysis.BuildTool = "Vite";
                analysis.DetectedTechnologies.Add("Vite Config");
            }

            // Astro
            if (fileNames.Contains("astro.config.mjs"))
            {
                analysis.Framework = "Astro";
                analysis.IsStatic = true;
            }

            // Svelte
            if (fileNames.Contains("svelte.config.js"))
            {
                analysis.Framework = analysis.Framework ?? "Svelte";
            }

            // Angular
            if (fileNames.Contains("angular.json"))
            {
                analysis.Framework = "Angular";
            }
        }

        private void DetectStaticSite(IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            // Check for index.html in root
            if (fileNames.Contains("index.html") && !fileNames.Contains("package.json"))
            {
                analysis.IsStatic = true;
                analysis.Framework = "Static HTML";
            }
        }

        private async Task DetectFrameworkSpecificFiles(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            // Check for pages/app directory (Next.js)
            if (fileNames.Contains("pages") || fileNames.Contains("app"))
            {
                try
                {
                    var pagesDir = contents.FirstOrDefault(c => c.Name.Equals("pages", StringComparison.OrdinalIgnoreCase));
                    if (pagesDir != null && pagesDir.Type == "dir")
                    {
                        var pagesContents = await _githubClient.Repository.Content.GetAllContents(owner, repoName, "pages");
                        if (pagesContents.Any(c => c.Name.Equals("api", StringComparison.OrdinalIgnoreCase)))
                        {
                            analysis.HasApiRoutes = true;
                            analysis.DetectedTechnologies.Add("API Routes");
                        }
                    }
                }
                catch { }
            }

            // Check for public directory
            if (fileNames.Contains("public"))
            {
                analysis.DetectedTechnologies.Add("Static Assets");
            }
        }

        private void GeneratePlatformSuggestions(RepositoryAnalysis analysis)
        {
            var suggestions = new List<PlatformSuggestion>();

            // Vercel Scoring
            var vercelScore = CalculateVercelScore(analysis);
            suggestions.Add(new PlatformSuggestion
            {
                Platform = "Vercel",
                Score = vercelScore.Score,
                Reason = vercelScore.Reason,
                DetectedFeatures = vercelScore.Features,
                IsRecommended = false
            });

            // Netlify Scoring
            var netlifyScore = CalculateNetlifyScore(analysis);
            suggestions.Add(new PlatformSuggestion
            {
                Platform = "Netlify",
                Score = netlifyScore.Score,
                Reason = netlifyScore.Reason,
                DetectedFeatures = netlifyScore.Features,
                IsRecommended = false
            });

            // Cloudflare Pages Scoring
            var cloudflareScore = CalculateCloudflareScore(analysis);
            suggestions.Add(new PlatformSuggestion
            {
                Platform = "Cloudflare Pages",
                Score = cloudflareScore.Score,
                Reason = cloudflareScore.Reason,
                DetectedFeatures = cloudflareScore.Features,
                IsRecommended = false
            });

            // GitHub Pages Scoring
            var githubPagesScore = CalculateGitHubPagesScore(analysis);
            suggestions.Add(new PlatformSuggestion
            {
                Platform = "GitHub Pages",
                Score = githubPagesScore.Score,
                Reason = githubPagesScore.Reason,
                DetectedFeatures = githubPagesScore.Features,
                IsRecommended = false
            });

            // Sort by score and mark the highest as recommended
            analysis.Suggestions = suggestions.OrderByDescending(s => s.Score).ToList();
            if (analysis.Suggestions.Any())
            {
                analysis.Suggestions[0].IsRecommended = true;
                analysis.RecommendedPlatform = analysis.Suggestions[0];
            }
        }

        private (int Score, string Reason, List<string> Features) CalculateVercelScore(RepositoryAnalysis analysis)
        {
            int score = 50; // Base score
            var features = new List<string>();
            var reasons = new List<string>();

            // Next.js is Vercel's framework
            if (analysis.Framework == "Next.js")
            {
                score += 40;
                features.Add("Next.js native support");
                reasons.Add("Vercel is built by the creators of Next.js");
            }

            // SSR support
            if (analysis.HasServerSideRendering)
            {
                score += 20;
                features.Add("Server-Side Rendering");
                reasons.Add("Excellent SSR and ISR support");
            }

            // API routes
            if (analysis.HasApiRoutes)
            {
                score += 15;
                features.Add("API Routes");
                reasons.Add("Serverless functions support");
            }

            // Edge functions
            if (analysis.HasEdgeFunctions)
            {
                score += 15;
                features.Add("Edge Functions");
                reasons.Add("Native edge runtime support");
            }

            // Other frameworks
            if (analysis.Framework == "React" || analysis.Framework == "Vue.js" || analysis.Framework == "Svelte")
            {
                score += 10;
                features.Add($"{analysis.Framework} support");
            }

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Good general-purpose platform";
            return (Math.Min(score, 100), reason, features);
        }

        private (int Score, string Reason, List<string> Features) CalculateNetlifyScore(RepositoryAnalysis analysis)
        {
            int score = 50;
            var features = new List<string>();
            var reasons = new List<string>();

            // Static sites
            if (analysis.IsStatic)
            {
                score += 30;
                features.Add("Static site hosting");
                reasons.Add("Excellent for static sites with CDN");
            }

            // Gatsby
            if (analysis.Framework == "Gatsby")
            {
                score += 25;
                features.Add("Gatsby optimization");
                reasons.Add("Optimized for Gatsby deployments");
            }

            // React, Vue, Angular
            if (analysis.Framework == "React" || analysis.Framework == "Vue.js" || analysis.Framework == "Angular")
            {
                score += 20;
                features.Add($"{analysis.Framework} support");
                reasons.Add("Great SPA support with redirects");
            }

            // Nuxt
            if (analysis.Framework == "Nuxt.js")
            {
                score += 15;
                features.Add("Nuxt.js support");
            }

            // Forms and functions
            score += 10;
            features.Add("Forms & Functions");
            reasons.Add("Built-in forms and serverless functions");

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Versatile platform for modern web apps";
            return (Math.Min(score, 100), reason, features);
        }

        private (int Score, string Reason, List<string> Features) CalculateCloudflareScore(RepositoryAnalysis analysis)
        {
            int score = 50;
            var features = new List<string>();
            var reasons = new List<string>();

            // Static sites
            if (analysis.IsStatic)
            {
                score += 25;
                features.Add("Static hosting");
                reasons.Add("Fast global CDN for static content");
            }

            // Edge functions
            if (analysis.HasEdgeFunctions)
            {
                score += 30;
                features.Add("Workers/Edge Functions");
                reasons.Add("Industry-leading edge computing");
            }

            // Modern frameworks
            if (analysis.Framework == "Astro" || analysis.Framework == "Svelte")
            {
                score += 20;
                features.Add($"{analysis.Framework} support");
                reasons.Add("Great support for modern frameworks");
            }

            // Next.js
            if (analysis.Framework == "Next.js")
            {
                score += 15;
                features.Add("Next.js support");
            }

            // React, Vue
            if (analysis.Framework == "React" || analysis.Framework == "Vue.js")
            {
                score += 15;
                features.Add($"{analysis.Framework} support");
            }

            features.Add("Global CDN");
            reasons.Add("Unlimited bandwidth on free tier");

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Fast and reliable hosting";
            return (Math.Min(score, 100), reason, features);
        }

        private (int Score, string Reason, List<string> Features) CalculateGitHubPagesScore(RepositoryAnalysis analysis)
        {
            int score = 40;
            var features = new List<string>();
            var reasons = new List<string>();

            // Static sites only
            if (analysis.IsStatic || analysis.Framework == "Static HTML")
            {
                score += 40;
                features.Add("Static site hosting");
                reasons.Add("Perfect for static HTML/CSS/JS sites");
            }

            // Jekyll
            if (analysis.DetectedTechnologies.Contains("Jekyll"))
            {
                score += 20;
                features.Add("Jekyll native support");
            }

            // React/Vue SPAs
            if (analysis.Framework == "React" || analysis.Framework == "Vue.js")
            {
                score += 15;
                features.Add($"{analysis.Framework} SPA");
                reasons.Add("Can host SPAs with proper configuration");
            }

            // Gatsby, Astro
            if (analysis.Framework == "Gatsby" || analysis.Framework == "Astro")
            {
                score += 20;
                features.Add($"{analysis.Framework} static output");
            }

            // Penalize SSR and API routes
            if (analysis.HasServerSideRendering || analysis.HasApiRoutes)
            {
                score -= 30;
                reasons.Add("No server-side rendering or API support");
            }

            features.Add("Free hosting");
            features.Add("GitHub integration");

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Simple and free for static sites";
            return (Math.Max(score, 0), reason, features);
        }
    }
}