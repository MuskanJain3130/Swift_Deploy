// Services/ITemplateEngine.cs

// Services/ITemplateEngine.cs
using SwiftDeploy.Models;

namespace SwiftDeploy.Services.Interfaces
{
    public interface ITemplateEngine
    {
        Task<string> GenerateConfigAsync(string platform, CommonConfig config);
        string GetConfigFileName(string platform);
        string GetContentType(string platform);
    }
}