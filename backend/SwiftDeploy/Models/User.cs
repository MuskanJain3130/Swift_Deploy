namespace SwiftDeploy.Models
{
    public class User
    {
        public string Id { get; set; }

        // GitHub OAuth users
        public string GithubId { get; set; }

        // Regular users (username/password)
        public string Username { get; set; }
        public string PasswordHash { get; set; } // Store hashed password, never plain text

        // Common fields for both user types
        public string Name { get; set; }
        public string Email { get; set; }
        public string AvatarUrl { get; set; }

        // User type identification
        public UserType UserType { get; set; } // GitHub or Regular

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

    public enum UserType
    {
        GitHub,
        Regular
    }
}