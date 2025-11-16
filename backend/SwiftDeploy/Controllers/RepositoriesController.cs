using Microsoft.AspNetCore.Authorization;
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

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
        /// Extracts and sets the GitHub access token from the Authorization header.
        /// </summary>
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

        /// <summary>
        /// Gets all repositories for the authenticated user.
        /// </summary>
        [HttpGet()]
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
        [HttpGet("{repoId}")]
        public IActionResult GetDeploymentsByRepo(string repoId)
        {
            var repositories = _mongo.Repositories.Find(r => r.Id == repoId).ToList();
            return Ok(repositories);
        }
    }
}