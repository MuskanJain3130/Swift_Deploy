namespace SwiftDeploy.Services
{
        public interface ITokenService
        {
            Task<string> GetPlatformTokenAsync(string userId, string platform, HttpContext context);
            Task<string> GetTokenFromHeaderAsync(HttpContext context, string platform);
        }
    
}
