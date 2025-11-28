//using SwiftDeploy.Models;

//namespace SwiftDeploy.Services.Interfaces
//{
//    public interface ILoggingService
//    {
//        Task LogAsync(LogEntry logEntry);
//        Task LogInfoAsync(string userId, string repositoryId, string deploymentId, string message, string platform = null);
//        Task LogWarningAsync(string userId, string repositoryId, string deploymentId, string message, string platform = null);
//        Task LogErrorAsync(string userId, string repositoryId, string deploymentId, string message, Exception ex = null, string platform = null);
//        Task LogDeploymentStatusAsync(string userId, string repositoryId, string deploymentId, DeploymentStatus status, string message, string platform = null);
//        Task<List<LogEntry>> GetLogsByDeploymentIdAsync(string deploymentId);
//        Task<List<LogEntry>> GetLogsByRepositoryIdAsync(string repositoryId);
//        Task<List<LogEntry>> GetLogsByUserIdAsync(string userId);
//        Task<List<LogEntry>> GetLogsByUserAndRepositoryAsync(string userId, string repositoryId);
//        Task<DeploymentStatistics> GetDeploymentStatisticsAsync(string userId, string repositoryId = null);
//    }
//}
