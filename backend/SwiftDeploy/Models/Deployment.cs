using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SwiftDeploy.Models
{
    public class Deployment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string UserId { get; set; }           // Link to User for querying
        public string Platform { get; set; }          // vercel, netlify, cloudflare, githubpages
        public string RepoId { get; set; }            // owner/repo format
        public string GitHubRepoUrl { get; set; }     // Full GitHub URL
        public string Status { get; set; }            // queued, processing, completed, failed
        public string ServiceId { get; set; }         // Platform-specific service/project ID
        public string ServiceUrl { get; set; }        // Deployed site URL
        public string ConfigFileUrl { get; set; }     // Config file URL in GitHub
        public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
        // Models/Deployment.cs

            // ⭐ NEW: Store platform-specific project ID
            [BsonElement("platformProjectId")]
            public string PlatformProjectId { get; set; }

            // ⭐ NEW: Store platform-specific project name (for Cloudflare, Netlify)
            [BsonElement("platformProjectName")]
            public string PlatformProjectName { get; set; }

            // ⭐ NEW: Store our internal project ID (GUID)
            [BsonElement("internalProjectId")]
            public string InternalProjectId { get; set; }
        }
    }

