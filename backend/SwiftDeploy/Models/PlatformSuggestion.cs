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
        public string Framework { get; set; }
        public string BuildTool { get; set; }
        public List<string> DetectedTechnologies { get; set; }
        public bool IsStatic { get; set; }
        public bool HasServerSideRendering { get; set; }
        public bool HasEdgeFunctions { get; set; }
        public bool HasApiRoutes { get; set; }
        public string PackageManager { get; set; }
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