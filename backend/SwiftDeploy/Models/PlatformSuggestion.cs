namespace SwiftDeploy.Models
{
        public class PlatformSuggestion
        {
            public string Platform { get; set; }
            public int Score { get; set; }
            public string Reason { get; set; }
            public List<string> DetectedFeatures { get; set; }
            public bool IsRecommended { get; set; }
        }

        public class RepositoryAnalysis
        {
            public string Owner { get; set; }
            public string RepoName { get; set; }

            // Project Type
            public string ProjectType { get; set; } // "Frontend", "Backend", "Full-Stack"

            // Language
            public string Language { get; set; } // "JavaScript/Node.js", "Python", "Java", "Go", etc.

            // Frontend
            public string Framework { get; set; }
            public string BuildTool { get; set; }
            public bool IsStatic { get; set; }
            public bool HasServerSideRendering { get; set; }

            // Backend
            public string BackendFramework { get; set; }
            public bool HasApiRoutes { get; set; }
            public bool HasDatabase { get; set; }
            public bool HasDocker { get; set; }

            // Advanced Features
            public bool HasEdgeFunctions { get; set; }

            // Build Tools
            public string PackageManager { get; set; }

            // Technologies
            public List<string> DetectedTechnologies { get; set; }

            // Suggestions
            public List<PlatformSuggestion> Suggestions { get; set; }
            public PlatformSuggestion RecommendedPlatform { get; set; }
        }

        public class PlatformSuggestionRequest
        {
            public string Owner { get; set; }
            public string RepoName { get; set; }
            public string Branch { get; set; } = "main";
        }
    }

