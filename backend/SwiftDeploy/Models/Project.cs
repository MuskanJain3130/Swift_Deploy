using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftDeploy.Models
{
    public class Project
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string UserId { get; set; } // Link to User
        public string ProjectName { get; set; }
        public string RepoId { get; set; }
        public string Branch { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
