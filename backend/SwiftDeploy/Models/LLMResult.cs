using System.Text.Json.Serialization;

namespace SwiftDeploy.Models
{
    public class LLMResult
    {
        public string Recommendation { get; set; }
        public List<LLMSuggestion> Suggestions { get; set; }
        public List<string> buildRisks { get; set; }          // ⭐ NEW
        public List<string> optimizations { get; set; }       // ⭐ NEW
    }

    public class LLMSuggestion
    {
        public string platform { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int score { get; set; }

        public string reason { get; set; }
    }
}
