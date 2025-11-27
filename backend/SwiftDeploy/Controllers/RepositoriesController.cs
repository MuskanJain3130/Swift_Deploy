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

            // Authenticate current user and obtain the stored GitHub token
            if (!TryAuthenticate(out var error)) return Unauthorized(error);

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
                // GitHub token is already set on _githubClient by TryAuthenticate,
                // but ensure credentials are set explicitly using the token returned

                Console.WriteLine($"=== Analyzing Repository ===");
                Console.WriteLine($"Owner: {request.Owner}");
                Console.WriteLine($"Repo: {request.RepoName}");
                Console.WriteLine($"Branch: {request.Branch ?? "main"}");
                //Console.WriteLine($"Token Length: {githubToken?.Length ?? 0}");
                //Console.WriteLine($"Token Prefix: {(githubToken != null ? githubToken.Substring(0, System.Math.Min(7, githubToken.Length)) + "..." : "NULL")}");

                // Test GitHub authentication first
                try
                {
                    var user = await _githubClient.User.Current();
                    Console.WriteLine($"Authenticated as: {user.Login}");
                }
                catch (System.Exception authEx)
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
                            technologies = analysis.DetectedTechnologies,
                            isStatic = analysis.IsStatic,
                            hasSSR = analysis.HasServerSideRendering,
                            hasEdgeFunctions = analysis.HasEdgeFunctions,
                            hasApiRoutes = analysis.HasApiRoutes,
                            hasDatabase = analysis.HasDatabase,
                            hasDocker = analysis.HasDocker
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
            catch (System.Exception ex)
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