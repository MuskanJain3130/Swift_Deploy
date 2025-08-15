using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftDeploy.Models
{
    public class Deployment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string  Id { get; set; }

        public string RepoId { get; set; }
        public string Status { get; set; } // queued, success, failed
        public string ServiceId { get; set; }
        public string ServiceUrl { get; set; }
        public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
    }
}
