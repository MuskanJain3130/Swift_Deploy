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

                // Detect package.json and analyze (Node.js)
                var packageJson = rootContents.FirstOrDefault(c => c.Name.Equals("package.json", StringComparison.OrdinalIgnoreCase));
                if (packageJson != null)
                {
                    await AnalyzePackageJson(owner, repoName, analysis);
                }

                // Detect Python projects
                await DetectPythonProject(owner, repoName, rootContents, analysis);

                // Detect Java/Spring projects
                await DetectJavaProject(owner, repoName, rootContents, analysis);

                // Detect .NET projects
                await DetectDotNetProject(owner, repoName, rootContents, analysis);

                // Detect Go projects
                await DetectGoProject(owner, repoName, rootContents, analysis);

                // Detect Ruby projects
                await DetectRubyProject(owner, repoName, rootContents, analysis);

                // Detect PHP projects
                await DetectPHPProject(owner, repoName, rootContents, analysis);

                // Detect Rust projects
                await DetectRustProject(owner, repoName, rootContents, analysis);

                // Detect Docker
                await DetectDocker(rootContents, analysis);

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

            // Frontend Framework detection
            if (depNames.Contains("next"))
            {
                analysis.Framework = "Next.js";
                analysis.DetectedTechnologies.Add("Next.js");
                analysis.HasServerSideRendering = true;
                analysis.HasApiRoutes = true;
                analysis.ProjectType = "Full-Stack";
            }
            else if (depNames.Contains("nuxt"))
            {
                analysis.Framework = "Nuxt.js";
                analysis.DetectedTechnologies.Add("Nuxt.js");
                analysis.HasServerSideRendering = true;
                analysis.ProjectType = "Full-Stack";
            }
            else if (depNames.Contains("gatsby"))
            {
                analysis.Framework = "Gatsby";
                analysis.DetectedTechnologies.Add("Gatsby");
                analysis.IsStatic = true;
                analysis.ProjectType = "Frontend";
            }
            else if (depNames.Contains("react"))
            {
                analysis.Framework = "React";
                analysis.DetectedTechnologies.Add("React");
                analysis.ProjectType = analysis.ProjectType ?? "Frontend";
            }
            else if (depNames.Contains("vue"))
            {
                analysis.Framework = "Vue.js";
                analysis.DetectedTechnologies.Add("Vue.js");
                analysis.ProjectType = analysis.ProjectType ?? "Frontend";
            }
            else if (depNames.Contains("svelte"))
            {
                analysis.Framework = "Svelte";
                analysis.DetectedTechnologies.Add("Svelte");
                analysis.ProjectType = analysis.ProjectType ?? "Frontend";
            }
            else if (depNames.Contains("astro"))
            {
                analysis.Framework = "Astro";
                analysis.DetectedTechnologies.Add("Astro");
                analysis.IsStatic = true;
                analysis.ProjectType = "Frontend";
            }
            else if (depNames.Contains("angular"))
            {
                analysis.Framework = "Angular";
                analysis.DetectedTechnologies.Add("Angular");
                analysis.ProjectType = analysis.ProjectType ?? "Frontend";
            }

            // Backend Framework detection (Node.js)
            if (depNames.Contains("express"))
            {
                analysis.BackendFramework = "Express.js";
                analysis.DetectedTechnologies.Add("Express.js");
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
                analysis.HasApiRoutes = true;
            }
            else if (depNames.Contains("fastify"))
            {
                analysis.BackendFramework = "Fastify";
                analysis.DetectedTechnologies.Add("Fastify");
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
                analysis.HasApiRoutes = true;
            }
            else if (depNames.Contains("koa"))
            {
                analysis.BackendFramework = "Koa";
                analysis.DetectedTechnologies.Add("Koa");
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
                analysis.HasApiRoutes = true;
            }
            else if (depNames.Contains("hapi") || depNames.Contains("@hapi/hapi"))
            {
                analysis.BackendFramework = "Hapi";
                analysis.DetectedTechnologies.Add("Hapi");
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
                analysis.HasApiRoutes = true;
            }
            else if (depNames.Contains("nestjs") || depNames.Contains("@nestjs/core"))
            {
                analysis.BackendFramework = "NestJS";
                analysis.DetectedTechnologies.Add("NestJS");
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
                analysis.HasApiRoutes = true;
            }
            else if (depNames.Contains("adonis") || depNames.Contains("@adonisjs/core"))
            {
                analysis.BackendFramework = "AdonisJS";
                analysis.DetectedTechnologies.Add("AdonisJS");
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
                analysis.HasApiRoutes = true;
            }

            // Database detection
            if (depNames.Contains("mongoose"))
            {
                analysis.DetectedTechnologies.Add("MongoDB (Mongoose)");
                analysis.HasDatabase = true;
            }
            else if (depNames.Contains("mongodb"))
            {
                analysis.DetectedTechnologies.Add("MongoDB");
                analysis.HasDatabase = true;
            }

            if (depNames.Contains("pg") || depNames.Contains("postgres"))
            {
                analysis.DetectedTechnologies.Add("PostgreSQL");
                analysis.HasDatabase = true;
            }

            if (depNames.Contains("mysql") || depNames.Contains("mysql2"))
            {
                analysis.DetectedTechnologies.Add("MySQL");
                analysis.HasDatabase = true;
            }

            if (depNames.Contains("sequelize"))
            {
                analysis.DetectedTechnologies.Add("Sequelize ORM");
                analysis.HasDatabase = true;
            }

            if (depNames.Contains("typeorm"))
            {
                analysis.DetectedTechnologies.Add("TypeORM");
                analysis.HasDatabase = true;
            }

            if (depNames.Contains("prisma") || depNames.Contains("@prisma/client"))
            {
                analysis.DetectedTechnologies.Add("Prisma ORM");
                analysis.HasDatabase = true;
            }

            if (depNames.Contains("redis") || depNames.Contains("ioredis"))
            {
                analysis.DetectedTechnologies.Add("Redis");
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
            if (depNames.Contains("graphql"))
                analysis.DetectedTechnologies.Add("GraphQL");
            if (depNames.Contains("socket.io"))
                analysis.DetectedTechnologies.Add("Socket.IO");
        }

        private async Task DetectPythonProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("requirements.txt") || fileNames.Contains("pipfile") || fileNames.Contains("pyproject.toml") || fileNames.Contains("setup.py"))
            {
                analysis.Language = "Python";
                analysis.DetectedTechnologies.Add("Python");
                analysis.ProjectType = "Backend";

                // Analyze requirements.txt
                if (fileNames.Contains("requirements.txt"))
                {
                    try
                    {
                        var reqContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "requirements.txt");
                        var reqString = System.Text.Encoding.UTF8.GetString(reqContent).ToLower();

                        if (reqString.Contains("django"))
                        {
                            analysis.BackendFramework = "Django";
                            analysis.DetectedTechnologies.Add("Django");
                            analysis.HasApiRoutes = true;
                        }
                        else if (reqString.Contains("flask"))
                        {
                            analysis.BackendFramework = "Flask";
                            analysis.DetectedTechnologies.Add("Flask");
                            analysis.HasApiRoutes = true;
                        }
                        else if (reqString.Contains("fastapi"))
                        {
                            analysis.BackendFramework = "FastAPI";
                            analysis.DetectedTechnologies.Add("FastAPI");
                            analysis.HasApiRoutes = true;
                        }
                        else if (reqString.Contains("tornado"))
                        {
                            analysis.BackendFramework = "Tornado";
                            analysis.DetectedTechnologies.Add("Tornado");
                            analysis.HasApiRoutes = true;
                        }
                        else if (reqString.Contains("pyramid"))
                        {
                            analysis.BackendFramework = "Pyramid";
                            analysis.DetectedTechnologies.Add("Pyramid");
                            analysis.HasApiRoutes = true;
                        }

                        // Database detection
                        if (reqString.Contains("psycopg2") || reqString.Contains("asyncpg"))
                        {
                            analysis.DetectedTechnologies.Add("PostgreSQL");
                            analysis.HasDatabase = true;
                        }
                        if (reqString.Contains("pymongo"))
                        {
                            analysis.DetectedTechnologies.Add("MongoDB");
                            analysis.HasDatabase = true;
                        }
                        if (reqString.Contains("sqlalchemy"))
                        {
                            analysis.DetectedTechnologies.Add("SQLAlchemy ORM");
                            analysis.HasDatabase = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task DetectJavaProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("pom.xml") || fileNames.Contains("build.gradle") || fileNames.Contains("build.gradle.kts"))
            {
                analysis.Language = "Java";
                analysis.DetectedTechnologies.Add("Java");
                analysis.ProjectType = "Backend";

                // Analyze pom.xml for Maven projects
                if (fileNames.Contains("pom.xml"))
                {
                    analysis.BuildTool = "Maven";
                    analysis.DetectedTechnologies.Add("Maven");

                    try
                    {
                        var pomContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "pom.xml");
                        var pomString = System.Text.Encoding.UTF8.GetString(pomContent).ToLower();

                        if (pomString.Contains("spring-boot"))
                        {
                            analysis.BackendFramework = "Spring Boot";
                            analysis.DetectedTechnologies.Add("Spring Boot");
                            analysis.HasApiRoutes = true;
                        }
                        else if (pomString.Contains("spring"))
                        {
                            analysis.BackendFramework = "Spring Framework";
                            analysis.DetectedTechnologies.Add("Spring Framework");
                            analysis.HasApiRoutes = true;
                        }
                        else if (pomString.Contains("quarkus"))
                        {
                            analysis.BackendFramework = "Quarkus";
                            analysis.DetectedTechnologies.Add("Quarkus");
                            analysis.HasApiRoutes = true;
                        }
                        else if (pomString.Contains("micronaut"))
                        {
                            analysis.BackendFramework = "Micronaut";
                            analysis.DetectedTechnologies.Add("Micronaut");
                            analysis.HasApiRoutes = true;
                        }
                    }
                    catch { }
                }

                // Gradle projects
                if (fileNames.Contains("build.gradle") || fileNames.Contains("build.gradle.kts"))
                {
                    analysis.BuildTool = "Gradle";
                    analysis.DetectedTechnologies.Add("Gradle");
                }
            }
        }

        private async Task DetectDotNetProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();
            var csprojFiles = contents.Where(c => c.Name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToList();

            if (csprojFiles.Any() || fileNames.Contains("global.json") || fileNames.Contains("nuget.config"))
            {
                analysis.Language = ".NET/C#";
                analysis.DetectedTechnologies.Add(".NET");
                analysis.ProjectType = "Backend";

                if (csprojFiles.Any())
                {
                    try
                    {
                        var csprojContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, csprojFiles.First().Name);
                        var csprojString = System.Text.Encoding.UTF8.GetString(csprojContent).ToLower();

                        if (csprojString.Contains("microsoft.aspnetcore"))
                        {
                            analysis.BackendFramework = "ASP.NET Core";
                            analysis.DetectedTechnologies.Add("ASP.NET Core");
                            analysis.HasApiRoutes = true;
                        }
                        else if (csprojString.Contains("microsoft.net.sdk.web"))
                        {
                            analysis.BackendFramework = "ASP.NET Core";
                            analysis.DetectedTechnologies.Add("ASP.NET Core");
                            analysis.HasApiRoutes = true;
                        }

                        if (csprojString.Contains("entityframeworkcore"))
                        {
                            analysis.DetectedTechnologies.Add("Entity Framework Core");
                            analysis.HasDatabase = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task DetectGoProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("go.mod") || fileNames.Contains("go.sum"))
            {
                analysis.Language = "Go";
                analysis.DetectedTechnologies.Add("Go");
                analysis.ProjectType = "Backend";

                if (fileNames.Contains("go.mod"))
                {
                    try
                    {
                        var goModContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "go.mod");
                        var goModString = System.Text.Encoding.UTF8.GetString(goModContent).ToLower();

                        if (goModString.Contains("gin-gonic/gin"))
                        {
                            analysis.BackendFramework = "Gin";
                            analysis.DetectedTechnologies.Add("Gin");
                            analysis.HasApiRoutes = true;
                        }
                        else if (goModString.Contains("gofiber/fiber"))
                        {
                            analysis.BackendFramework = "Fiber";
                            analysis.DetectedTechnologies.Add("Fiber");
                            analysis.HasApiRoutes = true;
                        }
                        else if (goModString.Contains("labstack/echo"))
                        {
                            analysis.BackendFramework = "Echo";
                            analysis.DetectedTechnologies.Add("Echo");
                            analysis.HasApiRoutes = true;
                        }
                        else if (goModString.Contains("gorilla/mux"))
                        {
                            analysis.BackendFramework = "Gorilla Mux";
                            analysis.DetectedTechnologies.Add("Gorilla Mux");
                            analysis.HasApiRoutes = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task DetectRubyProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("gemfile") || fileNames.Contains("gemfile.lock"))
            {
                analysis.Language = "Ruby";
                analysis.DetectedTechnologies.Add("Ruby");
                analysis.ProjectType = "Backend";

                if (fileNames.Contains("gemfile"))
                {
                    try
                    {
                        var gemfileContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "Gemfile");
                        var gemfileString = System.Text.Encoding.UTF8.GetString(gemfileContent).ToLower();

                        if (gemfileString.Contains("rails"))
                        {
                            analysis.BackendFramework = "Ruby on Rails";
                            analysis.DetectedTechnologies.Add("Ruby on Rails");
                            analysis.HasApiRoutes = true;
                        }
                        else if (gemfileString.Contains("sinatra"))
                        {
                            analysis.BackendFramework = "Sinatra";
                            analysis.DetectedTechnologies.Add("Sinatra");
                            analysis.HasApiRoutes = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task DetectPHPProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("composer.json") || fileNames.Contains("composer.lock"))
            {
                analysis.Language = "PHP";
                analysis.DetectedTechnologies.Add("PHP");
                analysis.ProjectType = "Backend";

                if (fileNames.Contains("composer.json"))
                {
                    try
                    {
                        var composerContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "composer.json");
                        var composerString = System.Text.Encoding.UTF8.GetString(composerContent).ToLower();

                        if (composerString.Contains("laravel/framework"))
                        {
                            analysis.BackendFramework = "Laravel";
                            analysis.DetectedTechnologies.Add("Laravel");
                            analysis.HasApiRoutes = true;
                        }
                        else if (composerString.Contains("symfony/symfony"))
                        {
                            analysis.BackendFramework = "Symfony";
                            analysis.DetectedTechnologies.Add("Symfony");
                            analysis.HasApiRoutes = true;
                        }
                        else if (composerString.Contains("codeigniter"))
                        {
                            analysis.BackendFramework = "CodeIgniter";
                            analysis.DetectedTechnologies.Add("CodeIgniter");
                            analysis.HasApiRoutes = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task DetectRustProject(string owner, string repoName, IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("cargo.toml") || fileNames.Contains("cargo.lock"))
            {
                analysis.Language = "Rust";
                analysis.DetectedTechnologies.Add("Rust");
                analysis.ProjectType = "Backend";

                if (fileNames.Contains("cargo.toml"))
                {
                    try
                    {
                        var cargoContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, "Cargo.toml");
                        var cargoString = System.Text.Encoding.UTF8.GetString(cargoContent).ToLower();

                        if (cargoString.Contains("actix-web"))
                        {
                            analysis.BackendFramework = "Actix Web";
                            analysis.DetectedTechnologies.Add("Actix Web");
                            analysis.HasApiRoutes = true;
                        }
                        else if (cargoString.Contains("rocket"))
                        {
                            analysis.BackendFramework = "Rocket";
                            analysis.DetectedTechnologies.Add("Rocket");
                            analysis.HasApiRoutes = true;
                        }
                        else if (cargoString.Contains("axum"))
                        {
                            analysis.BackendFramework = "Axum";
                            analysis.DetectedTechnologies.Add("Axum");
                            analysis.HasApiRoutes = true;
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task DetectDocker(IReadOnlyList<RepositoryContent> contents, RepositoryAnalysis analysis)
        {
            var fileNames = contents.Select(c => c.Name.ToLower()).ToList();

            if (fileNames.Contains("dockerfile") || fileNames.Contains("docker-compose.yml") || fileNames.Contains("docker-compose.yaml"))
            {
                analysis.DetectedTechnologies.Add("Docker");
                analysis.HasDocker = true;
            }
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

            // Detect backend start commands
            if (scriptValues.Any(s => s.Contains("node server") || s.Contains("nodemon")))
            {
                analysis.ProjectType = analysis.Framework != null ? "Full-Stack" : "Backend";
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
                analysis.ProjectType = "Frontend";
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

        //private void GeneratePlatformSuggestions(RepositoryAnalysis analysis)
        //{
        //    var suggestions = new List<PlatformSuggestion>();

        //    // Vercel Scoring
        //    var vercelScore = CalculateVercelScore(analysis);
        //    suggestions.Add(new PlatformSuggestion
        //    {
        //        Platform = "Vercel",
        //        Score = vercelScore.Score,
        //        Reason = vercelScore.Reason,
        //        DetectedFeatures = vercelScore.Features,
        //        IsRecommended = false
        //    });

        //    // Netlify Scoring
        //    var netlifyScore = CalculateNetlifyScore(analysis);
        //    suggestions.Add(new PlatformSuggestion
        //    {
        //        Platform = "Netlify",
        //        Score = netlifyScore.Score,
        //        Reason = netlifyScore.Reason,
        //        DetectedFeatures = netlifyScore.Features,
        //        IsRecommended = false
        //    });

        //    // Cloudflare Pages Scoring
        //    var cloudflareScore = CalculateCloudflareScore(analysis);
        //    suggestions.Add(new PlatformSuggestion
        //    {
        //        Platform = "Cloudflare Pages",
        //        Score = cloudflareScore.Score,
        //        Reason = cloudflareScore.Reason,
        //        DetectedFeatures = cloudflareScore.Features,
        //        IsRecommended = false
        //    });

        //    // GitHub Pages Scoring
        //    var githubPagesScore = CalculateGitHubPagesScore(analysis);
        //    suggestions.Add(new PlatformSuggestion
        //    {
        //        Platform = "GitHub Pages",
        //        Score = githubPagesScore.Score,
        //        Reason = githubPagesScore.Reason,
        //        DetectedFeatures = githubPagesScore.Features,
        //        IsRecommended = false
        //    });

        //    // Railway Scoring (for backend projects)
        //    var railwayScore = CalculateRailwayScore(analysis);
        //    if (railwayScore.Score > 0)
        //    {
        //        suggestions.Add(new PlatformSuggestion
        //        {
        //            Platform = "Railway",
        //            Score = railwayScore.Score,
        //            Reason = railwayScore.Reason,
        //            DetectedFeatures = railwayScore.Features,
        //            IsRecommended = false
        //        });
        //    }

        //    // Render Scoring (for backend projects)
        //    var renderScore = CalculateRenderScore(analysis);
        //    if (renderScore.Score > 0)
        //    {
        //        suggestions.Add(new PlatformSuggestion
        //        {
        //            Platform = "Render",
        //            Score = renderScore.Score,
        //            Reason = renderScore.Reason,
        //            DetectedFeatures = renderScore.Features,
        //            IsRecommended = false
        //        });
        //    }

        //    // Sort by score and mark the highest as recommended
        //    analysis.Suggestions = suggestions.OrderByDescending(s => s.Score).ToList();
        //    if (analysis.Suggestions.Any())
        //    {
        //        analysis.Suggestions[0].IsRecommended = true;
        //        analysis.RecommendedPlatform = analysis.Suggestions[0];
        //    }
        //}

        private (int Score, string Reason, List<string> Features) CalculateVercelScore(RepositoryAnalysis analysis)
        {
            int score = 70; // Base score
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

            // Backend frameworks - lower score
            if (analysis.ProjectType == "Backend" && analysis.BackendFramework != null)
            {
                score -= 20;
                reasons.Add("Better suited for frontend/full-stack with serverless functions");
            }

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Good general-purpose platform";
            return (Math.Max(Math.Min(score, 100), 0), reason, features);
        }

        private (int Score, string Reason, List<string> Features) CalculateNetlifyScore(RepositoryAnalysis analysis)
        {
            int score = 20;
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

            // Backend frameworks - lower score
            if (analysis.ProjectType == "Backend" && analysis.BackendFramework != null)
            {
                score -= 20;
                reasons.Add("Limited backend framework support");
            }

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Versatile platform for modern web apps";
            return (Math.Max(Math.Min(score, 100), 0), reason, features);
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

            // Backend frameworks - lower score
            if (analysis.ProjectType == "Backend" && analysis.BackendFramework != null)
            {
                score -= 20;
                reasons.Add("Primarily for frontend and edge computing");
            }

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Fast and reliable hosting";
            return (Math.Max(Math.Min(score, 100), 0), reason, features);
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

            // Backend frameworks - very low score
            if (analysis.ProjectType == "Backend")
            {
                score -= 40;
                reasons.Add("Does not support backend applications");
            }

            features.Add("Free hosting");
            features.Add("GitHub integration");

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Simple and free for static sites";
            return (Math.Max(score, 0), reason, features);
        }

        private (int Score, string Reason, List<string> Features) CalculateRailwayScore(RepositoryAnalysis analysis)
        {
            int score = 40;
            var features = new List<string>();
            var reasons = new List<string>();

            // Backend projects
            if (analysis.ProjectType == "Backend" || analysis.ProjectType == "Full-Stack")
            {
                score += 30;
                features.Add("Backend hosting");
                reasons.Add("Excellent for backend and full-stack applications");
            }

            // Database support
            if (analysis.HasDatabase)
            {
                score += 20;
                features.Add("Database support");
                reasons.Add("Built-in database provisioning");
            }

            // Docker support
            if (analysis.HasDocker)
            {
                score += 15;
                features.Add("Docker support");
                reasons.Add("Native Docker container deployment");
            }

            // Language-specific scoring
            if (analysis.Language == "Python" || analysis.Language == "Go" || analysis.Language == ".NET/C#" ||
                analysis.Language == "Java" || analysis.Language == "Ruby" || analysis.Language == "Rust")
            {
                score += 15;
                features.Add($"{analysis.Language} support");
                reasons.Add($"Great support for {analysis.Language} applications");
            }

            // Node.js backend
            if (analysis.BackendFramework != null && (analysis.BackendFramework.Contains("Express") ||
                analysis.BackendFramework.Contains("Fastify") || analysis.BackendFramework.Contains("NestJS")))
            {
                score += 15;
                features.Add($"{analysis.BackendFramework} support");
            }

            // Static sites - lower score
            if (analysis.IsStatic && analysis.ProjectType == "Frontend")
            {
                score -= 20;
                reasons.Add("Better platforms available for static sites");
            }

            features.Add("Auto-scaling");
            features.Add("CI/CD integration");

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Versatile platform for backend applications";
            return (Math.Max(Math.Min(score, 100), 0), reason, features);
        }

        private (int Score, string Reason, List<string> Features) CalculateRenderScore(RepositoryAnalysis analysis)
        {
            int score = 40;
            var features = new List<string>();
            var reasons = new List<string>();

            // Backend projects
            if (analysis.ProjectType == "Backend" || analysis.ProjectType == "Full-Stack")
            {
                score += 30;
                features.Add("Backend hosting");
                reasons.Add("Great for backend and full-stack applications");
            }

            // Database support
            if (analysis.HasDatabase)
            {
                score += 20;
                features.Add("Managed databases");
                reasons.Add("PostgreSQL, Redis support included");
            }

            // Docker support
            if (analysis.HasDocker)
            {
                score += 15;
                features.Add("Docker support");
                reasons.Add("Deploy any Dockerized application");
            }

            // Python frameworks
            if (analysis.BackendFramework == "Django" || analysis.BackendFramework == "Flask" || analysis.BackendFramework == "FastAPI")
            {
                score += 20;
                features.Add($"{analysis.BackendFramework} support");
                reasons.Add($"Excellent {analysis.BackendFramework} deployment experience");
            }

            // Node.js backend
            if (analysis.BackendFramework != null && (analysis.BackendFramework.Contains("Express") ||
                analysis.BackendFramework.Contains("Fastify") || analysis.BackendFramework.Contains("NestJS")))
            {
                score += 15;
                features.Add($"{analysis.BackendFramework} support");
            }

            // Ruby on Rails
            if (analysis.BackendFramework == "Ruby on Rails")
            {
                score += 20;
                features.Add("Ruby on Rails support");
                reasons.Add("Native Rails deployment support");
            }

            // Static sites
            if (analysis.IsStatic)
            {
                score += 10;
                features.Add("Static site hosting");
            }

            // Frontend only - lower score
            if (analysis.ProjectType == "Frontend" && !analysis.IsStatic)
            {
                score -= 15;
                reasons.Add("Better suited for backend applications");
            }

            features.Add("Free SSL");
            features.Add("Auto-deploy from Git");

            var reason = reasons.Any() ? string.Join(". ", reasons) : "Reliable platform for web services";
            return (Math.Max(Math.Min(score, 100), 0), reason, features);
        }
    }
}
