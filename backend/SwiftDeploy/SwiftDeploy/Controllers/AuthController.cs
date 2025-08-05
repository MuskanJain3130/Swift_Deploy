//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;



//namespace SwiftDeploy.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class AuthController : ControllerBase
//    {
//        [HttpGet("github/login")]
//        public IActionResult GitHubLogin()
//        { 
//           return Challenge(new AuthenticationProperties { RedirectUri = "http://localhost:5173/" }, "GitHub");
//        }

//        [HttpGet("github/callback")]
//        public async Task<IActionResult> GitHubCallback()
//        {
//           var result = await HttpContext.AuthenticateAsync();
//           var accessToken = await HttpContext.GetTokenAsync("access_token");

//           return Ok(new
//           {
//                token = accessToken,
//                username = result.Principal.Identity.Name
//           });
//        }
//    }   
//}



//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using Octokit; // You'll need to install this NuGet package
//using Microsoft.AspNetCore.Authentication.OAuth;

//namespace SwiftDeploy.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class AuthController : ControllerBase
//    {
//        private readonly IConfiguration _configuration;
//        private readonly HttpClient _httpClient;

//        public AuthController(IConfiguration configuration)
//        {
//            _configuration = configuration;
//            _httpClient = new HttpClient();
//        }

//        [HttpGet("github/login")]
//        public IActionResult GitHubLogin()
//        {
//            // The RedirectUri here is crucial. It tells the middleware where to send the user AFTER authentication.
//            return Challenge(new AuthenticationProperties { RedirectUri = "http://localhost:5173/repos" }, "GitHub");
//        }

//        [HttpGet("github/callback")]
//        public async Task<IActionResult> GitHubCallback()
//        {
//            var result = await HttpContext.AuthenticateAsync();
//            var accessToken = await HttpContext.GetTokenAsync("access_token");

//            // This part is the issue. The browser is not going to receive this JSON.
//            // The Challenge middleware has already redirected the user to http://localhost:5173/.
//            // This action is never reached directly by the user's browser.
//            return Ok(new
//            {
//                token = accessToken,
//                username = result.Principal.Identity.Name
//            });
//        }

//        [HttpGet("github/redirect")]
//        public IActionResult GitHubRedirect()
//        {
//            var clientId = _configuration["GitHub:ClientId"];
//            var redirectUri = _configuration["GitHub:RedirectUri"];
//            var scope = "repo";

//            // Construct the GitHub authorization URL
//            var url = $"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={redirectUri}&scope={scope}";

//            return Redirect(url);
//        }


//    }

//    public class GitHubTokenResponse
//    {
//        public string AccessToken { get; set; }
//        public string TokenType { get; set; }
//        public string Scope { get; set; }
//    }
//}




using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Text.Json;
using System.Net.Http.Headers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
    }

    [HttpGet("github/redirect")]
    public IActionResult GitHubRedirect()
    {
        var clientId = _configuration["GitHub:ClientId"];
        var redirectUri = _configuration["GitHub:RedirectUri"];
        var scope = "repo";

        // Construct the GitHub authorization URL
        var url = $"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={redirectUri}&scope={scope}";

        return Redirect(url);
    }

    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("GitHub authorization code not found.");
        }

        var clientId = _configuration["GitHub:ClientId"];
        var clientSecret = _configuration["GitHub:ClientSecret"];

        // Exchange the code for an access token on the server-side
        var requestBody = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<GitHubTokenResponse>();
        if (tokenResponse?.AccessToken == null)
        {
            return BadRequest("Failed to retrieve access token from GitHub.");
        }

        // At this point, the token is in your backend.
        // We now redirect to the React frontend with the token.
        // This is the most reliable way to pass the token.
        var frontendRedirectUri = "http://localhost:5173/auth-callback";
        return Redirect($"{frontendRedirectUri}?token={tokenResponse.AccessToken}");
    }

    private class GitHubTokenResponse
    {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public string Scope { get; set; }
    }
}