// Controllers/ConfigGeneratorController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftDeploy.Models;
using SwiftDeploy.Services.Interfaces;

namespace SwiftDeploy.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class ConfigGeneratorController : ControllerBase
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly IGitHubService _gitHubService;
        private readonly ILogger<ConfigGeneratorController> _logger;

        public ConfigGeneratorController(
            ITemplateEngine templateEngine,
            IGitHubService gitHubService,
            ILogger<ConfigGeneratorController> logger)
        {
            _templateEngine = templateEngine;
            _gitHubService = gitHubService;
            _logger = logger;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateConfig([FromBody] ConfigRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var supportedPlatforms = new[] { "vercel", "cloudflare", "githubpages", "netlify" };
                if (!supportedPlatforms.Contains(request.Platform.ToLower()))
                {
                    return BadRequest($"Unsupported platform: {request.Platform}. Supported platforms: {string.Join(", ", supportedPlatforms)}");
                }

                _logger.LogInformation($"Generating config for platform: {request.Platform}");

                var configContent = await _templateEngine.GenerateConfigAsync(request.Platform, request.Config);
                var fileName = _templateEngine.GetConfigFileName(request.Platform);
                var contentType = _templateEngine.GetContentType(request.Platform);

                var response = new ConfigResponse
                {
                    Platform = request.Platform,
                    FileName = fileName,
                    Content = configContent,
                    ContentType = contentType
                };

                _logger.LogInformation($"Successfully generated config for {request.Platform}");
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Invalid argument: {ex.Message}");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating config for platform: {request.Platform}");
                return StatusCode(500, "An error occurred while generating the configuration file.");
            }
        }

        [HttpPost("generate-and-save")]
        public async Task<IActionResult> GenerateAndSaveToGitHub([FromBody] GitHubConfigRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var supportedPlatforms = new[] { "vercel", "cloudflare", "githubpages", "netlify" };
                if (!supportedPlatforms.Contains(request.Platform.ToLower()))
                {
                    return BadRequest($"Unsupported platform: {request.Platform}. Supported platforms: {string.Join(", ", supportedPlatforms)}");
                }

                _logger.LogInformation($"Generating and saving config for platform: {request.Platform} to GitHub repo: {request.GitHubRepo}");

                // Generate config content
                var configContent = await _templateEngine.GenerateConfigAsync(request.Platform, request.Config);
                var fileName = _templateEngine.GetConfigFileName(request.Platform);

                // Save to GitHub
                var result = await _gitHubService.SaveFileToRepoAsync(
                    request.GitHubRepo,
                    fileName,
                    configContent,
                    request.CommitMessage ?? $"Add {request.Platform} configuration",
                    request.Branch ?? "main",
                    request.GitHubToken
                );

                var response = new GitHubSaveResponse
                {
                    Platform = request.Platform,
                    FileName = fileName,
                    GitHubRepo = request.GitHubRepo,
                    Branch = request.Branch ?? "main",
                    CommitSha = result.CommitSha,
                    FileUrl = result.FileUrl,
                    Success = result.Success,
                    Message = result.Message
                };

                if (result.Success)
                {
                    _logger.LogInformation($"Successfully saved config to GitHub: {result.FileUrl}");
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning($"Failed to save config to GitHub: {result.Message}");
                    return BadRequest(response);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Invalid argument: {ex.Message}");
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"GitHub authorization failed: {ex.Message}");
                return Unauthorized("GitHub authorization failed. Please check your access token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving config to GitHub for platform: {request.Platform}");
                return StatusCode(500, "An error occurred while saving the configuration file to GitHub.");
            }
        }

        [HttpGet("download/{platform}")]
        public async Task<IActionResult> DownloadConfig(string platform, [FromQuery] CommonConfig config)
        {
            try
            {
                var supportedPlatforms = new[] { "vercel", "cloudflare", "githubpages", "netlify" };
                if (!supportedPlatforms.Contains(platform.ToLower()))
                {
                    return BadRequest($"Unsupported platform: {platform}. Supported platforms: {string.Join(", ", supportedPlatforms)}");
                }

                var configContent = await _templateEngine.GenerateConfigAsync(platform, config);
                var fileName = _templateEngine.GetConfigFileName(platform);
                var contentType = _templateEngine.GetContentType(platform);

                var bytes = System.Text.Encoding.UTF8.GetBytes(configContent);
                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading config for platform: {platform}");
                return StatusCode(500, "An error occurred while generating the download file.");
            }
        }

        [HttpGet("platforms")]
        public IActionResult GetSupportedPlatforms()
        {
            var platforms = new[]
            {
                new { name = "vercel", fileName = "vercel.json", contentType = "application/json" },
                new { name = "cloudflare", fileName = "wrangler.toml", contentType = "text/plain" },
                new { name = "githubpages", fileName = ".github/workflows/deploy.yml", contentType = "text/yaml" },
                new { name = "netlify", fileName = "netlify.toml", contentType = "text/plain" }
            };

            return Ok(platforms);
        }
    }
}