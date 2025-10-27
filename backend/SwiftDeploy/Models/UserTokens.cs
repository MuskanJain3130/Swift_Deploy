namespace SwiftDeploy.Models
{// Models/UserTokens.cs
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    namespace SwiftDeploy.Models
    {
        public class UserTokens
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string Id { get; set; }

            [BsonElement("userId")]
            public string UserId { get; set; }

            [BsonElement("netlifyToken")]
            public string NetlifyToken { get; set; }

            [BsonElement("vercelToken")]
            public string VercelToken { get; set; }

            [BsonElement("cloudflareToken")]
            public string CloudflareToken { get; set; }

            [BsonElement("githubToken")]
            public string GitHubToken { get; set; }

            [BsonElement("createdAt")]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            [BsonElement("updatedAt")]
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }
    }
}
