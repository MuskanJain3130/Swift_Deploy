using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Repository
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string UserId { get; set; } // Link to User
        public string RepoName { get; set; }
        public string RepoUrl { get; set; }
        public string Branch { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }


