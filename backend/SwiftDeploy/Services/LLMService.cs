    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;

    namespace SwiftDeploy.Services
    {
        public class LLMService
        {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public LLMService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _apiKey = config["OpenRouter:ApiKey"];
        }

        public async Task<string> GetPlatformSuggestion(string prompt)
        {
            var requestBody = new
            {
                model = "deepseek/deepseek-chat", // ✅ FREE MODEL
                messages = new[]
                {
                new { role = "user", content = prompt }
            },
                temperature = 0.2
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Add("HTTP-Referer", "http://localhost:5000");
            request.Headers.Add("X-Title", "SwiftDeploy");

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine("=== OPENROUTER RAW ===");
            Console.WriteLine(responseString);

            return responseString;
        }
    }
    }

