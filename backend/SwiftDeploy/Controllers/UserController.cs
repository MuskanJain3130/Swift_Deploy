using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Octokit;
using Octokit.Internal;
using SwiftDeploy.Models;
using SwiftDeploy.Models.SwiftDeploy.Models;
using SwiftDeploy.Services;
using System.Security.Claims;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly MongoDbService _mongo;
        private readonly JwtHelper _jwtHelper;

        public UserController(MongoDbService mongo, JwtHelper jwtHelper)
        {
            _mongo = mongo;
            _jwtHelper = jwtHelper;
        }
        [HttpPost("register/github")]
        public IActionResult CreateUser(SwiftDeploy.Models.User user)
        {
            user.UserType = UserType.GitHub;
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            if (user == null)
            {
                return BadRequest("User data is required");
            }

            _mongo.Users.InsertOne(user);
            return Ok("User created");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _mongo.Users.Find(x =>
                    x.Username == request.Username || x.Email == request.Email).FirstOrDefaultAsync();

                if (existingUser != null)
                    return BadRequest("Username or email already exists");

                // Hash password
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                var user = new Models.User
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Username = request.Username,
                    Email = request.Email,
                    Name = request.Name,
                    PasswordHash = passwordHash,
                    UserType = UserType.Regular,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _mongo.Users.InsertOneAsync(user);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "User registered successfully",
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Name = user.Name,
                        Email = user.Email,
                        UserType = user.UserType
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                // Find user by username or email
                var user = await _mongo.Users.Find(x =>
                    x.Username == request.UsernameOrEmail || x.Email == request.UsernameOrEmail)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Unauthorized("Invalid credentials");

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                    return Unauthorized("Invalid credentials");

                // Generate JWT token
                var token = _jwtHelper.GenerateToken(user);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Name = user.Name,
                        Email = user.Email,
                        UserType = user.UserType
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        private async Task<Models.User> GetGitHubUserInfo(string accessToken)
        {
            var productInformation = new ProductHeaderValue("SwiftDeploy");
            var credentials = new Credentials(accessToken); // use the token here

            //var client = new GitHubClient(productInformation)
            //{
            //    Credentials = credentials
            //};

            //var user = await client.User.Current(); // gets authenticated user

            var client = new GitHubClient(new ProductHeaderValue("SwiftDeploy"))
            {
                Credentials = new Credentials(accessToken)
            };

            var user = await client.User.Current();
            var emails = await client.User.Email.GetAll();
            var primaryEmail = emails.FirstOrDefault(e => e.Primary && e.Verified)?.Email;

           var newuser = new Models.User
            {
                GithubId = user.Id.ToString(),
                Username = user.Login,
                Name = string.IsNullOrEmpty(user.Name) ? user.Login : user.Name,
                Email = primaryEmail,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return newuser;
        }
        [HttpPost("login/github/callback")]
        public async Task<IActionResult> GitHubOAuthCallback()
        {
            var accessToken = Request.Headers["Authorization"].ToString();

            if (string.IsNullOrWhiteSpace(accessToken))
                return BadRequest("Missing Authorization header");

            // Optional: strip "Bearer " prefix if present
            if (accessToken.StartsWith("Bearer "))
                accessToken = accessToken.Substring("Bearer ".Length);

            var gitHubUser = await GetGitHubUserInfo(accessToken);
            var user = await _mongo.Users.Find(x => x.GithubId == gitHubUser.GithubId.ToString()).FirstOrDefaultAsync();

            if (user == null)
            {
                user = new Models.User
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    GithubId = gitHubUser.GithubId.ToString(),
                    Email = gitHubUser.Email,
                    Name = gitHubUser.Name,
                    UserType = UserType.GitHub,
                    CreatedAt = DateTime.UtcNow,
                    AvatarUrl = gitHubUser.AvatarUrl,
                    UpdatedAt = DateTime.UtcNow
                };
                await _mongo.Users.InsertOneAsync(user);
                return Ok(new { requiresProfileCompletion = true, userId = user.Id });
            }

            if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.PasswordHash))
            {
                return Ok(new { requiresProfileCompletion = true, userId = user.Id });
            }

            var jwtToken = _jwtHelper.GenerateToken(user);
            return Ok(new { token = jwtToken, user });
        }


        [HttpPost("tokens")]
        public IActionResult CreateUserTokens(UserTokens userTokens)
        {
            _mongo.UserTokens.InsertOne(userTokens);
            return Ok("User tokens created");
        }
        [HttpPost("{userId}/tokens/{platform}")]
        public async Task<IActionResult> UpdatePlatformToken(string userId, string platform, [FromBody] string token)
        {
            // Validate platform
            var supportedPlatforms = new[] { "netlify", "vercel", "cloudflare", "github" };
            if (!supportedPlatforms.Contains(platform.ToLower()))
                return BadRequest("Unsupported platform.");

            // Find existing user tokens document
            var userTokens = await _mongo.UserTokens.Find(x => x.UserId == userId).FirstOrDefaultAsync();

            if (userTokens == null)
            {
                // If no tokens document exists for user, create one with a new ObjectId
                userTokens = new UserTokens 
                { 
                    Id = ObjectId.GenerateNewId().ToString(),
                    UserId = userId 
                };
            }

            // Set the relevant platform token
            switch (platform.ToLower())
            {
                case "netlify":
                    userTokens.NetlifyToken = token;
                    break;
                case "vercel":
                    userTokens.VercelToken = token;
                    break;
                case "cloudflare":
                    userTokens.CloudflareToken = token;
                    break;
                case "github":
                    userTokens.GitHubToken = token;
                    break;
            }

            // Update or Insert in MongoDB
            await _mongo.UserTokens.ReplaceOneAsync(
                x => x.UserId == userId,
                userTokens,
                new ReplaceOptions { IsUpsert = true });

            return Ok(new { success = true, message = $"Token for {platform} updated." });
        }

        [HttpPost("complete-profile")]
        public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileRequest request)
        {
            string userId = request.UserId;
            string username = request.Username;
            string password = request.Password;

            var user = await _mongo.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

            var existingUser = await _mongo.Users.Find(x => x.Username == username && x.Id != userId).FirstOrDefaultAsync();
            if (existingUser != null)
                return BadRequest("Username already exists");

            user.Username = username;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.UpdatedAt = DateTime.UtcNow;

            await _mongo.Users.ReplaceOneAsync(x => x.Id == userId, user);

            return Ok(new { success = true, message = "Profile completed successfully" });
        }


        [HttpGet("{userId}/tokens")]
        public IActionResult GetUserTokens(string userId)
        {
            var tokens = _mongo.UserTokens.Find(x => x.UserId == userId).FirstOrDefault();
            if (tokens == null)
                return NotFound("No tokens found for user");

            return Ok(new
            {
                userId = tokens.UserId,
                hasNetlifyToken = !string.IsNullOrEmpty(tokens.NetlifyToken),
                hasVercelToken = !string.IsNullOrEmpty(tokens.VercelToken),
                hasCloudflareToken = !string.IsNullOrEmpty(tokens.CloudflareToken),
                hasGitHubToken = !string.IsNullOrEmpty(tokens.GitHubToken)
            });
        }

        // Protected endpoint example
        [HttpGet("profile")]
        [Authorize] // Requires JWT token
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _mongo.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();

            if (user == null)
                return NotFound("User not found");

            return Ok(new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Name = user.Name,
                Email = user.Email,
                UserType = user.UserType
            });
        }
    }
}
