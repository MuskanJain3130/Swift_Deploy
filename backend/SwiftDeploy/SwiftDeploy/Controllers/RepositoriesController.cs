using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Linq;
using System.Threading.Tasks;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepositoriesController : ControllerBase
    {
        private readonly GitHubClient _githubClient;

        public RepositoriesController()
        {
            _githubClient = new GitHubClient(new ProductHeaderValue("SwiftDeployApp"));
        }

        /// <summary>
        /// Extracts and sets the GitHub access token from the Authorization header.
        /// </summary>
        private bool TryAuthenticate(out string errorMessage)
        {
            errorMessage = string.Empty;
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                errorMessage = "Missing or invalid Authorization header.";
                return false;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                errorMessage = "GitHub access token not found. Please re-authenticate.";
                return false;
            }

            _githubClient.Credentials = new Credentials(token);
            return true;
        }

        /// <summary>
        /// Gets all repositories for the authenticated user.
        /// </summary>
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
    }
}