using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Linq;
using System.Security.Claims;
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
            // The GitHubClient needs a ProductHeaderValue to identify your app
            _githubClient = new GitHubClient(new ProductHeaderValue("SwiftDeployApp"));
        }

        [HttpGet]
        public async Task<IActionResult> GetRepositories()
        {
            // This is the key part: getting the token from the Authorization header
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Missing or invalid Authorization header.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            // Use the token to authenticate with the GitHub API
            _githubClient.Credentials = new Credentials(accessToken);

            try
            {
                // Call the GitHub API to get the user's repositories
                var repos = await _githubClient.Repository.GetAllForCurrent();

                // Return the list of repositories
                return Ok(repos);
            }
            catch (AuthorizationException ex)
            {
                // Handle cases where the token is invalid or expired
                return Unauthorized("GitHub authorization failed. " + ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a specific repository by its owner and name.
        /// </summary>
        /// <param name="owner">The owner of the repository (e.g., 'octocat').</param>
        /// <param name="repoName">The name of the repository (e.g., 'Spoon-Knife').</param>
        /// <returns>A single repository object or a 404 if not found.</returns>
        [HttpGet("{owner}/{repoName}")] // Defines the route for this action: /api/repositories/{owner}/{repoName}
        public async Task<IActionResult> GetRepository(string owner, string repoName)
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Missing or invalid Authorization header.");
            }

            var githubAccessToken = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(githubAccessToken))
            {
                return Unauthorized("GitHub access token not found in claims. Please re-authenticate.");
            }

            _githubClient.Credentials = new Credentials(githubAccessToken);

            try
            {
                // Call the GitHub API to get a specific repository
                var repo = await _githubClient.Repository.Get(owner, repoName);

                // Return the repository details
                return Ok(repo);
            }
            catch (NotFoundException)
            {
                // Octokit throws NotFoundException if the repository does not exist or is private
                return NotFound($"Repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                return Unauthorized("GitHub API authorization failed for specific repository. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching repository '{owner}/{repoName}': {ex.Message}");
                return StatusCode(500, $"An internal server error occurred: {ex.Message}");
            }
        }


        /// <summary>
        /// Gets the contents (files and directories) of a specific path within a repository.
        /// </summary>
        /// <param name="owner">The owner of the repository.</param>
        /// <param name="repoName">The name of the repository.</param>
        /// <param name="path">The path within the repository (e.g., 'src/components', or empty for root).</param>
        /// <returns>A list of repository content items (files, directories) or a 404 if path not found.</returns>
        [HttpGet("{owner}/{repoName}/contents/{*path}")] // Route for contents, {*path} captures the rest of the URL as a path
        public async Task<IActionResult> GetRepositoryContents(string owner, string repoName, string? path)
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Missing or invalid Authorization header.");
            }

            var githubAccessToken = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(githubAccessToken))
            {
                return Unauthorized("GitHub access token not found in claims. Please re-authenticate.");
            }
            _githubClient.Credentials = new Credentials(githubAccessToken);

            try
            {
                // Octokit's GetAllContents method can list files and directories at a given path.
                // If 'path' is null or empty, it will list the root contents of the repository.
                var contents = await _githubClient.Repository.Content.GetAllContents(owner, repoName, path ?? string.Empty);

                // Return the list of contents. Each item will indicate if it's a file, directory, etc.
                return Ok(contents);
            }
            catch (NotFoundException)
            {
                // This indicates the path does not exist in the repository or the user lacks access.
                return NotFound($"Contents for path '{path ?? "root"}' in repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                // Authorization failure related to GitHub API access for contents.
                return Unauthorized("GitHub API authorization failed for repository contents. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching contents for '{owner}/{repoName}/{path}': {ex.Message}");
                return StatusCode(500, $"An internal server error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the raw content of a specific file within a repository.
        /// </summary>
        /// <param name="owner">The owner of the repository.</param>
        /// <param name="repoName">The name of the repository.</param>
        /// <param name="path">The full path to the file within the repository (e.g., 'src/App.js').</param>
        /// <returns>The raw content of the file as plain text.</returns>
        [HttpGet("{owner}/{repoName}/file/{*path}")] // New route for file content: /api/repositories/{owner}/{repoName}/file/{path_to_file}
        public async Task<IActionResult> GetRepositoryFileContent(string owner, string repoName, string path)
        {
            var githubAccessToken = User.FindFirst("access_token")?.Value;

            if (string.IsNullOrEmpty(githubAccessToken))
            {
                return Unauthorized("GitHub access token not found in claims. Please re-authenticate.");
            }

            _githubClient.Credentials = new Credentials(githubAccessToken);

            try
            {
                // Octokit's GetRawContent method fetches the raw content of a file.
                // It returns the content as a byte array.
                var fileContent = await _githubClient.Repository.Content.GetRawContent(owner, repoName, path);

                // Convert the byte array to a string using UTF-8 encoding.
                var fileContentAsString = System.Text.Encoding.UTF8.GetString(fileContent);

                // Return the content as plain text.
                return Content(fileContentAsString, "text/plain");
            }
            catch (NotFoundException)
            {
                // This indicates the file does not exist at the given path or the user lacks access.
                return NotFound($"File '{path}' in repository '{owner}/{repoName}' not found or you do not have access.");
            }
            catch (AuthorizationException ex)
            {
                // Authorization failure related to GitHub API access for the file.
                return Unauthorized("GitHub API authorization failed for file content. " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching file content for '{owner}/{repoName}/{path}': {ex.Message}");
                return StatusCode(500, $"An internal server error occurred: {ex.Message}");
            }
        }
    }
}
