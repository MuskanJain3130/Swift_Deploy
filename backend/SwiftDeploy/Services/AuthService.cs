//// Services/AuthService.cs
//using Microsoft.Extensions.Configuration;
//using Microsoft.IdentityModel.Tokens;
//using MongoDB.Driver;
//using System;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;
//using SwiftDeploy.Models;
//using SwiftDeploy.DTOs;

//namespace SwiftDeploy.Services
//{
//    public class AuthService
//    {
//        private readonly IMongoCollection<User> _users;
//        private readonly IConfiguration _configuration;

//        public AuthService(IMongoDatabase database, IConfiguration configuration)
//        {
//            _users = database.GetCollection<User>("Users");
//            _configuration = configuration;
//        }

//        public async Task<User> Register(RegisterDto registerDto)
//        {
//            if (await _users.Find(u => u.Email == registerDto.Email).AnyAsync())
//                throw new Exception("Email already exists");

//            CreatePasswordHash(registerDto.Password, out byte[] passwordHash, out byte[] passwordSalt);

//            var user = new User
//            {
//                Name = registerDto.Name,
//                Email = registerDto.Email.ToLower(),
//                PasswordHash = passwordHash,
//                PasswordSalt = passwordSalt,
//                EmailVerificationToken = CreateRandomToken(),
//                EmailVerificationExpiry = DateTime.UtcNow.AddDays(1)
//            };

//            await _users.InsertOneAsync(user);
            
//            // TODO: Send verification email
//            return user;
//        }

//        public async Task<AuthResponseDto> Login(LoginDto loginDto)
//        {
//            var user = await _users.Find(u => u.Email == loginDto.Email.ToLower()).FirstOrDefaultAsync();
//            if (user == null || !VerifyPasswordHash(loginDto.Password, user.PasswordHash, user.PasswordSalt))
//                throw new Exception("Invalid credentials");

//            if (!user.EmailVerified)
//                throw new Exception("Please verify your email first");

//            user.LastLogin = DateTime.UtcNow;
//            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

//            return CreateAuthResponse(user);
//        }

//        public async Task<AuthResponseDto> GitHubAuth(string githubId, string email, string name)
//        {
//            var user = await _users.Find(u => u.GithubId == githubId || u.Email == email).FirstOrDefaultAsync();
            
//            if (user == null)
//            {
//                // Create new user with GitHub
//                user = new User
//                {
//                    GithubId = githubId,
//                    Email = email.ToLower(),
//                    Name = name,
//                    EmailVerified = true // GitHub verifies email
//                };
                
//                await _users.InsertOneAsync(user);
//            }
//            else if (user.GithubId == null)
//            {
//                // Link GitHub to existing account
//                user.GithubId = githubId;
//                await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
//            }

//            user.LastLogin = DateTime.UtcNow;
//            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

//            return CreateAuthResponse(user);
//        }

//        public async Task<bool> SetPassword(string userId, string newPassword)
//        {
//            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
//            if (user == null) return false;

//            CreatePasswordHash(newPassword, out byte[] passwordHash, out byte[] passwordSalt);
            
//            user.PasswordHash = passwordHash;
//            user.PasswordSalt = passwordSalt;
            
//            var result = await _users.ReplaceOneAsync(u => u.Id == userId, user);
//            return result.IsAcknowledged && result.ModifiedCount > 0;
//        }

//        private AuthResponseDto CreateAuthResponse(User user)
//        {
//            return new AuthResponseDto
//            {
//                Token = GenerateJwtToken(user),
//                User = new UserDto
//                {
//                    Id = user.Id,
//                    Name = user.Name,
//                    Email = user.Email,
//                    EmailVerified = user.EmailVerified,
//                    HasPassword = user.PasswordHash != null,
//                    HasGithub = !string.IsNullOrEmpty(user.GithubId)
//                }
//            };
//        }

//        // Helper methods
//        private string GenerateJwtToken(User user)
//        {
//            var tokenHandler = new JwtSecurityTokenHandler();
//            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);

//            var tokenDescriptor = new SecurityTokenDescriptor
//            {
//                Subject = new ClaimsIdentity(new[]
//                {
//                    new Claim(ClaimTypes.NameIdentifier, user.Id),
//                    new Claim(ClaimTypes.Email, user.Email),
//                    new Claim(ClaimTypes.Name, user.Name),
//                    // Add other claims as needed
//                }),
//                Expires = DateTime.UtcNow.AddDays(7),
//                SigningCredentials = new SigningCredentials(
//                    new SymmetricSecurityKey(key),
//                    SecurityAlgorithms.HmacSha256Signature)
//            };

//            var token = tokenHandler.CreateToken(tokenDescriptor);
//            return tokenHandler.WriteToken(token);
//        }

//        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
//        {
//            using var hmac = new HMACSHA512();
//            passwordSalt = hmac.Key;
//            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
//        }

//        private bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
//        {
//            if (storedHash == null || storedSalt == null)
//                return false;

//            using var hmac = new HMACSHA512(storedSalt);
//            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
//            for (int i = 0; i < computedHash.Length; i++)
//            {
//                if (computedHash[i] != storedHash[i]) return false;
//            }
//            return true;
//        }

//        private string CreateRandomToken()
//        {
//            return Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
//        }
//    }
//}