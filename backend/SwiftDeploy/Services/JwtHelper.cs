// Services/JwtHelper.cs
using Microsoft.IdentityModel.Tokens;
using SwiftDeploy.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SwiftDeploy.Services
{
    public class JwtHelper
    {
        private readonly IConfiguration _configuration;

        public JwtHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username ?? user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserType", user.UserType.ToString())
            };

            var expiresInHours = Convert.ToDouble(_configuration["Jwt:ExpiryInHours"]);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(expiresInHours),
                signingCredentials: credentials
            );

            // Log token details for debugging
            Console.WriteLine($"Generated JWT token. Expires at: {token.ValidTo} (UTC)");

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}