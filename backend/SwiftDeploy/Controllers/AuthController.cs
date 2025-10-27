using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;



namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpGet("github/login")]
        public IActionResult GitHubLogin()
        {  // You can accept the frontend's redirect URI as a query parameter.
            // If none is provided, use a default.
            string redirectUri = "http://localhost:5173/auth-callback";
            // The Challenge method will save this RedirectUri in the state parameter.
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, "GitHub");

        }

        [HttpGet("github/callback")]
        public async Task<IActionResult> GitHubCallback()
        {
            // The authentication will handle the correlation cookie automatically.
            // If successful, the result will contain the authenticated principal.
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // This is a check to ensure authentication was successful.
            if (result?.Principal == null)
            {
                return BadRequest("Authentication failed.");
            }

            // Since you are returning a JSON response, you should remove any final redirects.
            var accessToken = result.Properties.GetTokenValue("access_token");

            // Sign in the user with a cookie.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                result.Principal,
                result.Properties);
            return Ok(new
            {
                token = accessToken,
                username = result.Principal.Identity.Name,
                RedirectUri = "http://localhost:5173/auth-callback"
            });
        }
    }
}

