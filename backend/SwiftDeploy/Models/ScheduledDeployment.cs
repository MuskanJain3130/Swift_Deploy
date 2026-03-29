using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftDeploy.Models
{
    public class ScheduledDeployment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string ProjectId { get; set; }   // Link to Project
        public string UserId { get; set; }

        public string DeploymentType { get; set; } // "upload" | "github"

        public string Platform { get; set; }
        public DateTime ScheduledTime { get; set; }

        public string Status { get; set; } = "Pending";
        // Pending, Running, Completed, Failed

        public bool IsExecuted { get; set; } = false;

        public string PayloadJson { get; set; } // store request

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
