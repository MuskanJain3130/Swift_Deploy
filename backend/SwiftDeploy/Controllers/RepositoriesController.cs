using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Models.SwiftDeploy.Models;
using SwiftDeploy.Services;
using SwiftDeploy.Services.Interfaces;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepositoriesController : ControllerBase
    {
        private readonly GitHubClient _githubClient;
        private readonly MongoDbService _mongo;
        //private readonly ILoggingService _loggingService;

        private readonly LLMService _llmService;

        public RepositoriesController(MongoDbService mongo, LLMService llmService)
        {
            _githubClient = new GitHubClient(new ProductHeaderValue("SwiftDeployApp"));
            _mongo = mongo;
            _llmService = llmService;
        }

        /// <summary>
        /// Authenticate user and (optionally) return stored GitHub token.
        /// </summary>
        //private bool TryAuthenticate(out string errorMessage, out string githubToken)
        //{
        //    errorMessage = string.Empty;
        //    githubToken = null;

        //    // Extract user ID from JWT claims
        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        errorMessage = "User ID not found in token.";
        //        return false;
        //    }

        //    // Look up the GitHub token from MongoDB using UserId field
        //    var userToken = _mongo.UserTokens.Find(x => x.UserId == userId).FirstOrDefault();
        //    if (userToken == null || string.IsNullOrEmpty(userToken.GitHubToken))
        //    {
        //        errorMessage = "GitHub token not found in database. Please re-authenticate with GitHub.";
        //        return false;
        //    }

        //    // Set the GitHub token for Octokit and return it
        //    githubToken = userToken.GitHubToken;
        //    _githubClient.Credentials = new Credentials(githubToken);
        //    return true;
        //}

        private bool TryAuthenticate(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Extract user ID from JWT claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                errorMessage = "User ID not found in token.";
                return false;
            }
            // Look up the GitHub token from MongoDB using UserId field
            var userToken = _mongo.UserTokens.Find(x => x.UserId == userId).FirstOrDefault();
            if (userToken == null || string.IsNullOrEmpty(userToken.GitHubToken))
            {
                errorMessage = "GitHub token not found in database. Please re-authenticate with GitHub.";
                return false;
            }

            // Set the GitHub token for Octokit
            _githubClient.Credentials = new Credentials(userToken.GitHubToken);
            return true;
        }

        [HttpGet]
        public async Task<IActionResult> GetRepositories()
        {
            if (!TryAuthenticate(out var error)) return Unauthorized(error);

            try
            {
                var repos = await _githubClient.Repository.GetAllForCurrent();
                return Ok(repos);
            }
            catch (AuthorizationException ex)
            {
                return Unauthorized("GitHub authorization failed. " + ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a specific repository by owner and name.
        /// </summary>
        [HttpGet("{owner}/{repoName}")]
        public async Task<IActionResult> GetRepository(string owner, string repoName)
        {
            if (!TryAuthenticate(out var error)) return Unauthorized(error);

            try
            {
                var repo = await _githubClient.Repository.Get(owner, repoName);
                return Ok(repo);
            }
            catch (NotFoundException)
            {
                return NotFound($"Repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                return Unauthorized("GitHub API authorization failed. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching repository '{owner}/{repoName}': {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the contents of the root directory of a repository.
        /// </summary>
        [HttpGet("contents/{owner}/{repoName}")]
        public async Task<IActionResult> GetRootContents(string owner, string repoName)
        {
            if (!TryAuthenticate(out var error)) return Unauthorized(error);

            try
            {
                var contents = await _githubClient.Repository.Content.GetAllContents(owner, repoName);
                return Ok(contents);
            }
            catch (NotFoundException)
            {
                return NotFound($"Repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                return Unauthorized("GitHub API authorization failed. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching root contents: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the contents of a specific path within a repository.
        /// </summary>
        [HttpGet("contents/{owner}/{repoName}/{*path}")]
        public async Task<IActionResult> GetContentsByPath(string owner, string repoName, string path)
        {
            if (!TryAuthenticate(out var error)) return Unauthorized(error);

            try
            {
                var contents = await _githubClient.Repository.Content.GetAllContents(owner, repoName, path);
                return Ok(contents);
            }
            catch (NotFoundException)
            {
                return NotFound($"Path '{path}' in repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                return Unauthorized("GitHub API authorization failed. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching contents for path '{path}': {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the raw content of a specific file within a repository.
        /// </summary>
        [HttpGet("file/{owner}/{repoName}/{*path}")]
        public async Task<IActionResult> GetFileContent(string owner, string repoName, string path)
        {
            if (!TryAuthenticate(out var error)) return Unauthorized(error);

            try
            {
                var fileContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, path);
                var contentAsString = System.Text.Encoding.UTF8.GetString(fileContent);
                return Content(contentAsString, "text/plain");
            }
            catch (NotFoundException)
            {
                return NotFound($"File '{path}' in repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                return Unauthorized("GitHub API authorization failed. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching file content for '{path}': {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("save")]
        public IActionResult SaveRepository([FromBody] System.Collections.Generic.Dictionary<string, object> body)
        {
            if (body == null)
                return BadRequest("Repository data is required");

            var repo = new Repository
            {
                RepoName = body.ContainsKey("repoName") ? body["repoName"].ToString() : null,
                RepoUrl = body.ContainsKey("repoUrl") ? body["repoUrl"].ToString() : null,
                Branch = body.ContainsKey("branch") ? body["branch"].ToString() : "main",
                UserId = body.ContainsKey("userId") ? body["userId"].ToString() : null,
                CreatedAt = System.DateTime.UtcNow
            };

            _mongo.Repositories.InsertOne(repo);
            return Ok(new { message = "Repository saved", repo });
        }

        [HttpGet("saved")]
        public IActionResult GetSavedRepositories()
        {
            var repos = _mongo.Repositories.Find(_ => true).ToList();
            return Ok(repos);
        }

        [HttpGet("deployments/{repoId}")]
        public IActionResult GetDeploymentsByRepo(string repoId)
        {
            var repositories = _mongo.Repositories.Find(r => r.Id == repoId).ToList();
            return Ok(repositories);
        }

        /// <summary>
        /// Analyzes a repository and suggests the best deployment platform
        /// </summary>
        [HttpPost("analyze-and-suggest")]
        public async Task<IActionResult> AnalyzeAndSuggestPlatform([FromBody] PlatformSuggestionRequest request)
        {
            Console.WriteLine("=== Analyze and Suggest Endpoint Called ===");

            if (!TryAuthenticate(out var error))
                return Unauthorized(error);

            if (request == null || string.IsNullOrEmpty(request.Owner) || string.IsNullOrEmpty(request.RepoName))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Missing required information: Owner and RepoName are required."
                });
            }

            try
            {
                Console.WriteLine($"=== Analyzing Repository ===");
                Console.WriteLine($"Owner: {request.Owner}");
                Console.WriteLine($"Repo: {request.RepoName}");
                Console.WriteLine($"Branch: {request.Branch ?? "main"}");

                // ✅ Test GitHub authentication
                try
                {
                    var user = await _githubClient.User.Current();
                    Console.WriteLine($"Authenticated as: {user.Login}");
                }
                catch (Exception authEx)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        error = "GitHub authentication failed",
                        details = authEx.Message
                    });
                }

                // ✅ Analyze repository
                var analyzer = new RepositoryAnalyzerService(_githubClient);
                var analysis = await analyzer.AnalyzeRepository(
                    request.Owner,
                    request.RepoName,
                    request.Branch ?? "main"
                );

                // ✅ Improved prompt
                var prompt = $@"
                    You are a senior DevOps architect.

                    Analyze the project and provide:

                    1. Best deployment platform
                    2. Score for each platform
                    3. Possible build failures
                    4. Optimization suggestions

                    Project:
                    Language: {analysis.Language}
                    Frontend: {analysis.Framework}
                    Backend: {analysis.BackendFramework}
                    Build Tool: {analysis.BuildTool}
                    Package Manager: {analysis.PackageManager}

                    Features:
                    Static: {analysis.IsStatic}
                    SSR: {analysis.HasServerSideRendering}
                    API: {analysis.HasApiRoutes}
                    Database: {analysis.HasDatabase}
                    Docker: {analysis.HasDocker}
                    Technologies: {string.Join(", ", analysis.DetectedTechnologies ?? new List<string>())}

                    Platforms:
                    - Vercel
                    - Netlify
                    - Cloudflare Pages
                    - GitHub Pages

                    Rules:
                    - SSR → Vercel preferred
                    - Static → Netlify/Cloudflare preferred
                    - API/Backend → avoid GitHub Pages

                    Return ONLY JSON:
                    {{
                      ""recommendation"": ""string"",
                      ""suggestions"": [
                        {{
                          ""platform"": ""string"",
                          ""score"": number,
                          ""reason"": ""string""
                        }}
                      ],
                      ""buildRisks"": [
                        ""string""
                      ],
                      ""optimizations"": [
                        ""string""
                      ]
                    }}";

                // ✅ Call LLM
                var llmRaw = await _llmService.GetPlatformSuggestion(prompt);

                Console.WriteLine("=== LLM RAW RESPONSE ===");
                Console.WriteLine(llmRaw);

                // ✅ SAFE PARSING (FIXED)
                string content = null;

                using (var jsonDoc = JsonDocument.Parse(llmRaw))
                {
                    var root = jsonDoc.RootElement;

                    // Case 1: OpenAI format
                    if (root.TryGetProperty("choices", out var choices))
                    {
                        content = choices[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString();
                    }
                    else
                    {
                        // Case 2: Direct JSON
                        content = llmRaw;
                    }
                }

                // ✅ Extract clean JSON
                var cleanJson = ExtractJson(content);

                Console.WriteLine("=== CLEAN JSON ===");
                Console.WriteLine(cleanJson);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var llmResult = JsonSerializer.Deserialize<LLMResult>(cleanJson, options);

                if (llmResult == null || llmResult.Suggestions == null)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        error = "LLM parsing failed"
                    });
                }

                Console.WriteLine($"✅ LLM Recommendation: {llmResult.Recommendation}");

                // ✅ FINAL RESPONSE (LLM BASED)
                return Ok(new
                {
                    success = true,
                    analysis = new
                    {
                        repository = new
                        {
                            owner = analysis.Owner,
                            name = analysis.RepoName
                        },
                        projectInfo = new
                        {
                            type = analysis.ProjectType ?? "Unknown",
                            language = analysis.Language ?? "Unknown",
                            frontendFramework = analysis.Framework ?? "None",
                            backendFramework = analysis.BackendFramework ?? "None"
                        },
                        detectedTechnologies = new
                        {
                            buildTool = analysis.BuildTool ?? "None",
                            packageManager = analysis.PackageManager ?? "None",
                            isStatic = analysis.IsStatic,
                            hasSSR = analysis.HasServerSideRendering,
                            hasApiRoutes = analysis.HasApiRoutes,
                            hasDatabase = analysis.HasDatabase,
                            hasDocker = analysis.HasDocker
                        },

                        // ⭐ EXISTING
                        recommendedPlatform = llmResult.Recommendation,

                        allSuggestions = llmResult.Suggestions.Select(s => new
                        {
                            platform = s.platform,
                            score = s.score,
                            reason = s.reason,
                            isRecommended = s.platform == llmResult.Recommendation
                        }),

                        // ⭐ NEW FEATURES
                        buildRisks = llmResult.buildRisks ?? new List<string>(),

                        optimizations = llmResult.optimizations ?? new List<string>()
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                Console.WriteLine($"STACK: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    success = false,
                    error = "An error occurred while analyzing the repository.",
                    details = ex.Message
                });
            }
        }
        private string ExtractJson(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var start = text.IndexOf("{");
            var end = text.LastIndexOf("}");

            if (start >= 0 && end >= 0 && end > start)
                return text.Substring(start, end - start + 1);

            return text;
        }
    }
}