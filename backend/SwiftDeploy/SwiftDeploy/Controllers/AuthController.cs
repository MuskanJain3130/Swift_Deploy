using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;



namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpGet("github/login")]
        public IActionResult GitHubLogin()
        {
           return Challenge(new AuthenticationProperties { RedirectUri = "/" }, "GitHub");
        }

            [HttpGet("github/callback")]
            public async Task<IActionResult> GitHubCallback()
            {
                var result = await HttpContext.AuthenticateAsync();
                var accessToken = await HttpContext.GetTokenAsync("access_token");

                return Ok(new
                {
                    token = accessToken,
                    username = result.Principal.Identity.Name
                });
            }
        }
    
}
