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
    }
}
