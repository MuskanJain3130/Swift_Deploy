using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftDeploy.Models
{
    public class LogEntry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        // Identifiers - linking to your existing models
        [BsonElement("userId")]
        public string UserId { get; set; }

        [BsonElement("repositoryId")]
        public string RepositoryId { get; set; }

        [BsonElement("deploymentId")]
        public string DeploymentId { get; set; }

        // Log Details
        [BsonElement("message")]
        public string Message { get; set; }

        [BsonElement("level")]
        public LogLevel Level { get; set; } = LogLevel.Info;

        [BsonElement("category")]
        public LogCategory Category { get; set; } = LogCategory.Deployment;

        [BsonElement("status")]
        public DeploymentStatus Status { get; set; }

        [BsonElement("platform")]
        public string Platform { get; set; }

        [BsonElement("repositoryName")]
        public string RepositoryName { get; set; }

        [BsonElement("branch")]
        public string Branch { get; set; }

        [BsonElement("error")]
        public ErrorDetails Error { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("durationMs")]
        public long DurationMs { get; set; }

        [BsonElement("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        // Denormalized user info (optional but helpful)
        [BsonElement("userName")]
        public string UserName { get; set; }

        [BsonElement("userEmail")]
        public string UserEmail { get; set; }
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    public enum LogCategory
    {
        Authentication,
        Authorization,
        Deployment,
        Configuration,
        RepositoryAnalysis,
        FileUpload,
        GitHubOperation,
        PlatformAPI,
        Database,
        System
    }

    //public enum DeploymentStatus
    //{
    //    Initiated,
    //    Uploading,
    //    Processing,
    //    Analyzing,
    //    CreatingRepository,
    //    PushingCode,
    //    GeneratingConfig,
    //    Deploying,
    //    Success,
    //    Failed,
    //    Cancelled
    //}

    public class ErrorDetails
    {
        [BsonElement("errorCode")]
        public string ErrorCode { get; set; }

        [BsonElement("errorMessage")]
        public string ErrorMessage { get; set; }

        [BsonElement("stackTrace")]
        public string StackTrace { get; set; }

        [BsonElement("source")]
        public string Source { get; set; }

        [BsonElement("additionalInfo")]
        public Dictionary<string, string> AdditionalInfo { get; set; } = new Dictionary<string, string>();
    }
}