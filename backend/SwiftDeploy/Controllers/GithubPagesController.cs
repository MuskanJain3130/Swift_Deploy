using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/githubpages")]
public class GitHubPagesController : ControllerBase
{
    private const string GitHubApiBaseUrl = "https://api.github.com";

    [HttpPost("enable")]
    public async Task<IActionResult> EnableGitHubPagesSite(
        [FromHeader(Name = "GitHub-Token")] string gitHubToken,
        [FromBody] GitHubPagesRequest request)
    {
        if (string.IsNullOrWhiteSpace(gitHubToken))
            return BadRequest("GitHub-Token header is required.");

        if (request == null ||
            string.IsNullOrWhiteSpace(request.Owner) ||
            string.IsNullOrWhiteSpace(request.Repo) ||
            string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest("Owner, Repo, and Branch are required in the body.");

        using var client = new HttpClient();
        client.BaseAddress = new Uri(GitHubApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gitHubToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("YourAppName"); // GitHub API requires User-Agent header
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        string pagesEndpoint = $"/repos/{request.Owner}/{request.Repo}/pages";

        // Step 1: Check if Pages site exists (GET)
        var getResponse = await client.GetAsync(pagesEndpoint);
        if (getResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Step 2a: Create Pages site (POST)
            var createPayload = new
            {
                source = new
                {
                    branch = request.Branch,
                    path = request.Path ?? "/"  // default to root
                },
                build_type = request.BuildType ?? "legacy" // or "workflow"
            };
            var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
            var postResponse = await client.PostAsync(pagesEndpoint, createContent);
            var postResult = await postResponse.Content.ReadAsStringAsync();

            if (!postResponse.IsSuccessStatusCode)
                return StatusCode((int)postResponse.StatusCode, postResult);
        }
        else if (getResponse.IsSuccessStatusCode)
        {
            // Step 2b: Update Pages site (PUT)
            var updatePayload = new
            {
                source = new
                {
                    branch = request.Branch,
                    path = request.Path ?? "/"
                },
                build_type = request.BuildType ?? "legacy"
            };
            var updateContent = new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json");
            var putResponse = await client.PutAsync(pagesEndpoint, updateContent);

            if (!putResponse.IsSuccessStatusCode)
                return StatusCode((int)putResponse.StatusCode, await putResponse.Content.ReadAsStringAsync());
        }
        else
        {
            // Unexpected error
            return StatusCode((int)getResponse.StatusCode, await getResponse.Content.ReadAsStringAsync());
        }

        // Step 3 (Optional): Trigger a build to deploy the site immediately
        if (request.TriggerBuild)
        {
            var buildEndpoint = $"/repos/{request.Owner}/{request.Repo}/pages/builds";
            var buildResponse = await client.PostAsync(buildEndpoint, null);
            if (!buildResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)buildResponse.StatusCode, await buildResponse.Content.ReadAsStringAsync());
            }
        }

        return Ok(new { message = "GitHub Pages site enabled and configured successfully." });
    }
}

public class GitHubPagesRequest
{
    public string Owner { get; set; }       // GitHub repo owner
    public string Repo { get; set; }        // GitHub repo name
    public string Branch { get; set; }      // Branch to publish (e.g., "main")
    public string Path { get; set; }        // "/" or "/docs"
    public string BuildType { get; set; }   // "legacy" or "workflow"
    public bool TriggerBuild { get; set; }  // To optionally trigger immediate site build
}
