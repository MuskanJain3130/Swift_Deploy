using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/cloudflare")]
[Authorize]
public class CloudflarePagesController : ControllerBase
{
    private const string BaseApiUrl = "https://api.cloudflare.com/client/v4/accounts";
    private const string UserApiUrl = "https://api.cloudflare.com/client/v4/accounts";

    // Generates a unique project name using repo, branch, and timestamp
    private string GenerateProjectName(string owner, string repo, string branch)
    {
        string raw = $"{owner}-{repo}-{branch}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLower();
        return $"cfpages-{hex.Substring(0, 10)}";
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate(
        [FromHeader(Name = "Cloudflare-Api-Token")] string apiToken,
        [FromBody] RequestData requestData
    )
    {
        if (string.IsNullOrEmpty(apiToken))
            return BadRequest("Missing Cloudflare API token in headers.");
        if (requestData == null || string.IsNullOrEmpty(requestData.Owner) ||
            string.IsNullOrEmpty(requestData.RepoName) ||
            string.IsNullOrEmpty(requestData.Branch))
            return BadRequest("Missing required information: Owner, RepoName, or Branch.");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        // 1. Get Cloudflare account ID
        var userResponse = await client.GetAsync(UserApiUrl);
        var userString = await userResponse.Content.ReadAsStringAsync();
        if (!userResponse.IsSuccessStatusCode)
            return StatusCode((int)userResponse.StatusCode, userString);
        var userJson = JsonDocument.Parse(userString);
        string accountId = userJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

        // 2. Generate project name automatically
        string projectName = GenerateProjectName(requestData.Owner, requestData.RepoName, requestData.Branch);

        // 3. Prepare create payload (as per Cloudflare API spec)
        var createProjectUrl = $"{BaseApiUrl}/{accountId}/pages/projects";
        var createPayload = new
        {
            name = projectName,
            production_branch = requestData.Branch,
            source = new
            {
                type = "github",
                config = new
                {
                    git_provider = "github",
                    owner = requestData.Owner,
                    repo_name = requestData.RepoName,
                    branch = requestData.Branch
                }
            },
        };
        var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync(createProjectUrl, createContent);
        var createResult = await createResponse.Content.ReadAsStringAsync();
        if (!createResponse.IsSuccessStatusCode)
            return StatusCode((int)createResponse.StatusCode, createResult);

        // 4. Trigger deployment
        var deployUrl = $"{BaseApiUrl}/{accountId}/pages/projects/{projectName}/deployments";
        var deployResponse = await client.PostAsync(deployUrl, new StringContent("{}", Encoding.UTF8, "application/json"));
        var deployResult = await deployResponse.Content.ReadAsStringAsync();

        return Ok(new
        {
            accountId,
            projectName,
            createProject = JsonSerializer.Deserialize<object>(createResult),
            deployResult = JsonSerializer.Deserialize<object>(deployResult)
        });
    }
}


public class RequestData
{
    public string Owner { get; set; }               // "aarshiitaliya"
    public string RepoName { get; set; }            // "Test-Repository-static"
    public string Branch { get; set; }              // "main"
    public string BuildCommand { get; set; }        // "npm run build" or blank
    public string BuildDir { get; set; }            // "out" or "public"
}
