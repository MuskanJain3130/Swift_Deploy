using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Octokit;
using SwiftDeploy.Models;
using SwiftDeploy.Models.SwiftDeploy.Models;
using SwiftDeploy.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SwiftDeploy.Models;
using Microsoft.AspNetCore.Authorization;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepositoriesController : ControllerBase
    {
        private readonly GitHubClient _githubClient;
        private readonly MongoDbService _mongo;

        public RepositoriesController(MongoDbService mongo)
        {
            _githubClient = new GitHubClient(new ProductHeaderValue("SwiftDeployApp"));
            _mongo = mongo;
        }

        /// <summary>
        /// </summary>
        private bool TryAuthenticate(out string errorMessage)
        {
            errorMessage = string.Empty;

            {
                return false;
            }

            {
                return false;
            }

            return true;
        }
            catch (Exception ex)
            {
                errorMessage = $"Failed to set GitHub credentials: {ex.Message}";
                Console.WriteLine($"ERROR: Exception setting credentials - {ex.Message}");
                return false;
            }
        }

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
        public IActionResult SaveRepository([FromBody] Dictionary<string, object> body)
        {
            if (body == null)
                return BadRequest("Repository data is required");

            var repo = new Repository
            {
                RepoName = body.ContainsKey("repoName") ? body["repoName"].ToString() : null,
                RepoUrl = body.ContainsKey("repoUrl") ? body["repoUrl"].ToString() : null,
                Branch = body.ContainsKey("branch") ? body["branch"].ToString() : "main",
                UserId = body.ContainsKey("userId") ? body["userId"].ToString() : null,
                CreatedAt = DateTime.UtcNow
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

            // Try to get token from multiple sources
            var githubToken = HttpContext.Request.Headers["GitHub-API-Key"].FirstOrDefault()
                           ?? HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "")
                           ?? HttpContext.Request.Cookies["GitHubAccessToken"];

            Console.WriteLine($"GitHub-API-Key Header: {HttpContext.Request.Headers["GitHub-API-Key"].FirstOrDefault() ?? "NULL"}");
            Console.WriteLine($"Authorization Header: {HttpContext.Request.Headers["Authorization"].FirstOrDefault() ?? "NULL"}");
            Console.WriteLine($"Cookie Token: {HttpContext.Request.Cookies["GitHubAccessToken"] ?? "NULL"}");
            Console.WriteLine($"Final Token Found: {!string.IsNullOrEmpty(githubToken)}");

            // Validate GitHub token
            if (string.IsNullOrEmpty(githubToken))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Missing GitHub token. Please provide via GitHub-API-Key header, Authorization header, or GitHubAccessToken cookie.",
                    debug = new
                    {
                        hasGitHubApiKeyHeader = HttpContext.Request.Headers.ContainsKey("GitHub-API-Key"),
                        hasAuthorizationHeader = HttpContext.Request.Headers.ContainsKey("Authorization"),
                        hasGitHubCookie = HttpContext.Request.Cookies.ContainsKey("GitHubAccessToken")
                    }
                });
            }

            githubToken = githubToken.Trim();

            // Validate request body
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
                // Set GitHub credentials
                _githubClient.Credentials = new Credentials(githubToken);

                Console.WriteLine($"=== Analyzing Repository ===");
                Console.WriteLine($"Owner: {request.Owner}");
                Console.WriteLine($"Repo: {request.RepoName}");
                Console.WriteLine($"Branch: {request.Branch ?? "main"}");
                Console.WriteLine($"Token Length: {githubToken.Length}");
                Console.WriteLine($"Token Prefix: {githubToken.Substring(0, Math.Min(7, githubToken.Length))}...");

                // Test GitHub authentication first
                try
                {
                    var user = await _githubClient.User.Current();
                    Console.WriteLine($"Authenticated as: {user.Login}");
                }
                catch (Exception authEx)
                {
                    Console.WriteLine($"Authentication test failed: {authEx.Message}");
                    return Unauthorized(new
                    {
                        success = false,
                        error = "GitHub authentication failed. Token may be invalid or expired.",
                        details = authEx.Message
                    });
                }

                // Perform analysis
                var analyzer = new RepositoryAnalyzerService(_githubClient);
                var analysis = await analyzer.AnalyzeRepository(
                    request.Owner,
                    request.RepoName,
                    request.Branch ?? "main"
                );

                Console.WriteLine($"Analysis completed successfully");
                Console.WriteLine($"Framework detected: {analysis.Framework ?? "Unknown"}");
                Console.WriteLine($"Recommended platform: {analysis.RecommendedPlatform?.Platform ?? "None"}");

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
                        detectedTechnologies = new
                        {
                            framework = analysis.Framework ?? "Unknown",
                            buildTool = analysis.BuildTool ?? "None",
                            packageManager = analysis.PackageManager ?? "None",
                            technologies = analysis.DetectedTechnologies,
                            isStatic = analysis.IsStatic,
                            hasSSR = analysis.HasServerSideRendering,
                            hasEdgeFunctions = analysis.HasEdgeFunctions,
                            hasApiRoutes = analysis.HasApiRoutes
                        },
                        recommendedPlatform = analysis.RecommendedPlatform,
                        allSuggestions = analysis.Suggestions.Select(s => new
                        {
                            platform = s.Platform,
                            score = s.Score,
                            reason = s.Reason,
                            features = s.DetectedFeatures,
                            isRecommended = s.IsRecommended
                        })
                    }
                });
            }
            catch (AuthorizationException ex)
            {
                Console.WriteLine($"GitHub authorization failed: {ex.Message}");
                return Unauthorized(new
                {
                    success = false,
                    error = "GitHub authorization failed. Please check your GitHub token.",
                    details = ex.Message
                });
            }
            catch (NotFoundException ex)
            {
                Console.WriteLine($"Repository not found: {ex.Message}");
                return NotFound(new
                {
                    success = false,
                    error = $"Repository '{request.Owner}/{request.RepoName}' not found or you do not have access.",
                    details = ex.Message
                });
            }
            catch (RateLimitExceededException ex)
            {
                Console.WriteLine($"GitHub rate limit exceeded: {ex.Message}");
                return StatusCode(429, new
                {
                    success = false,
                    error = "GitHub API rate limit exceeded. Please try again later.",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing repository: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    error = "An error occurred while analyzing the repository.",
                    details = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}