using MongoDB.Driver;
using SwiftDeploy.Models;
using SwiftDeploy.Models.SwiftDeploy.Models;
using System.Globalization;

namespace SwiftDeploy.Services
{
    public class TokenService
    {
        private readonly IMongoCollection<UserTokens> _userTokensCollection;
        private readonly ILogger<TokenService> _logger;

        // Constructor using your MongoDbService
        public TokenService(MongoDbService mongoDbService, ILogger<TokenService> logger)
        {
            _userTokensCollection = mongoDbService.UserTokens;
            _logger = logger;
        }

        public async Task<string> GetPlatformTokenAsync(string userId, string platform, HttpContext context)
        {
            try
            {
                // Priority 1: Check headers
                var headerToken = await GetTokenFromHeaderAsync(context, platform);
                if (!string.IsNullOrEmpty(headerToken))
                {
                    _logger.LogInformation($"Found {platform} token in headers for user {userId}");
                    return headerToken;
                }

                // Priority 2: Check MongoDB
                var dbToken = await GetTokenFromDatabaseAsync(userId, platform);
                if (!string.IsNullOrEmpty(dbToken))
                {
                    _logger.LogInformation($"Found {platform} token in database for user {userId}");
                    return dbToken;
                }

                _logger.LogWarning($"No {platform} token found for user {userId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {platform} token for user {userId}");
                return null;
            }
        }

        public async Task<string> GetTokenFromHeaderAsync(HttpContext context, string platform)
        {
            try
            {
                // Check platform-specific header
                var textInfo = CultureInfo.InvariantCulture.TextInfo;
                var headerName = $"X-{textInfo.ToTitleCase(platform)}-Token";
                if (context.Request.Headers.TryGetValue(headerName, out var token))
                    return token.FirstOrDefault();

                // Check generic header
                if (context.Request.Headers.TryGetValue("X-Platform-Token", out var genericToken))
                    return genericToken.FirstOrDefault();

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading {platform} token from headers");
                return null;
            }
        }

        private async Task<string> GetTokenFromDatabaseAsync(string userId, string platform)
        {
            try
            {
                var userTokens = await _userTokensCollection
                    .Find(x => x.UserId == userId)
                    .FirstOrDefaultAsync();

                if (userTokens == null) return null;

                return platform.ToLower() switch
                {
                    "netlify" => userTokens.NetlifyToken,
                    "vercel" => userTokens.VercelToken,
                    "cloudflare" => userTokens.CloudflareToken,
                    "github" => userTokens.GitHubToken,
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching {platform} token from database for user {userId}");
                return null;
            }
        }
    }
}