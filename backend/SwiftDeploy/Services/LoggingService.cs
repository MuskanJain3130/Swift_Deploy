//using MongoDB.Driver;
//using SwiftDeploy.Data;
//using SwiftDeploy.Models;
//using SwiftDeploy.Services.Interfaces;
//using LogLevel = SwiftDeploy.Models.LogLevel;

//namespace SwiftDeploy.Services
//{
//    public class LoggingService : ILoggingService
//    {
//        private readonly IMongoCollection<LogEntry> _logs;
//        private readonly IMongoCollection<User> _users;

//        public LoggingService(MongoDbService mongoDbService)
//        {
//            _logs = mongoDbService.Logs;
//            _users = mongoDbService.Users;
//        }

//        public async Task LogAsync(LogEntry logEntry)
//        {
//            try
//            {
//                // Optionally get user info for denormalization
//                if (!string.IsNullOrEmpty(logEntry.UserId))
//                {
//                    var user = await _users.Find(u => u.Id == logEntry.UserId).FirstOrDefaultAsync();
//                    if (user != null)
//                    {
//                        logEntry.UserName = user.Name ?? user.Username;
//                        logEntry.UserEmail = user.Email;
//                    }
//                }

//                await _logs.InsertOneAsync(logEntry);

//                // Console logging for debugging
//                var logLevel = logEntry.Level.ToString().ToUpper();
//                var timestamp = logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
//                Console.WriteLine($"[{timestamp}] [{logLevel}] [{logEntry.Category}] User: {logEntry.UserName ?? "Unknown"} - {logEntry.Message}");

//                if (logEntry.Error != null)
//                {
//                    Console.WriteLine($"  Error: {logEntry.Error.ErrorMessage}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Failed to write log to database: {ex.Message}");
//            }
//        }

//        public async Task LogInfoAsync(string userId, string repositoryId, string deploymentId, string message, string platform = null)
//        {
//            await LogAsync(new LogEntry
//            {
//                UserId = userId,
//                RepositoryId = repositoryId,
//                DeploymentId = deploymentId,
//                Message = message,
//                Level = LogLevel.Info,
//                Category = LogCategory.Deployment,
//                Platform = platform,
//                Status = DeploymentStatus.Processing
//            });
//        }

//        public async Task LogWarningAsync(string userId, string repositoryId, string deploymentId, string message, string platform = null)
//        {
//            await LogAsync(new LogEntry
//            {
//                UserId = userId,
//                RepositoryId = repositoryId,
//                DeploymentId = deploymentId,
//                Message = message,
//                Level = LogLevel.Warning,
//                Category = LogCategory.Deployment,
//                Platform = platform
//            });
//        }

//        public async Task LogErrorAsync(string userId, string repositoryId, string deploymentId, string message, Exception ex = null, string platform = null)
//        {
//            var logEntry = new LogEntry
//            {
//                UserId = userId,
//                RepositoryId = repositoryId,
//                DeploymentId = deploymentId,
//                Message = message,
//                Level = LogLevel.Error,
//                Category = LogCategory.Deployment,
//                Platform = platform,
//                Status = DeploymentStatus.Failed
//            };

//            if (ex != null)
//            {
//                logEntry.Error = new ErrorDetails
//                {
//                    ErrorCode = ex.GetType().Name,
//                    ErrorMessage = ex.Message,
//                    StackTrace = ex.StackTrace,
//                    Source = ex.Source
//                };
//            }

//            await LogAsync(logEntry);
//        }

//        public async Task LogDeploymentStatusAsync(string userId, string repositoryId, string deploymentId, DeploymentStatus status, string message, string platform = null)
//        {
//            await LogAsync(new LogEntry
//            {
//                UserId = userId,
//                RepositoryId = repositoryId,
//                DeploymentId = deploymentId,
//                Message = message,
//                Level = status == DeploymentStatus.Failed ? LogLevel.Error : LogLevel.Info,
//                Category = LogCategory.Deployment,
//                Status = status,
//                Platform = platform
//            });
//        }

//        public async Task<List<LogEntry>> GetLogsByDeploymentIdAsync(string deploymentId)
//        {
//            var filter = Builders<LogEntry>.Filter.Eq(l => l.DeploymentId, deploymentId);
//            return await _logs.Find(filter).SortBy(l => l.Timestamp).ToListAsync();
//        }

//        public async Task<List<LogEntry>> GetLogsByRepositoryIdAsync(string repositoryId)
//        {
//            var filter = Builders<LogEntry>.Filter.Eq(l => l.RepositoryId, repositoryId);
//            return await _logs.Find(filter).SortByDescending(l => l.Timestamp).ToListAsync();
//        }

//        public async Task<List<LogEntry>> GetLogsByUserIdAsync(string userId)
//        {
//            var filter = Builders<LogEntry>.Filter.Eq(l => l.UserId, userId);
//            return await _logs.Find(filter).SortByDescending(l => l.Timestamp).ToListAsync();
//        }

//        public async Task<List<LogEntry>> GetLogsByUserAndRepositoryAsync(string userId, string repositoryId)
//        {
//            var filter = Builders<LogEntry>.Filter.And(
//                Builders<LogEntry>.Filter.Eq(l => l.UserId, userId),
//                Builders<LogEntry>.Filter.Eq(l => l.RepositoryId, repositoryId)
//            );
//            return await _logs.Find(filter).SortByDescending(l => l.Timestamp).ToListAsync();
//        }

//        public async Task<DeploymentStatistics> GetDeploymentStatisticsAsync(string userId, string repositoryId = null)
//        {
//            FilterDefinition<LogEntry> filter;

//            if (!string.IsNullOrEmpty(repositoryId))
//            {
//                filter = Builders<LogEntry>.Filter.And(
//                    Builders<LogEntry>.Filter.Eq(l => l.UserId, userId),
//                    Builders<LogEntry>.Filter.Eq(l => l.RepositoryId, repositoryId)
//                );
//            }
//            else
//            {
//                filter = Builders<LogEntry>.Filter.Eq(l => l.UserId, userId);
//            }

//            var logs = await _logs.Find(filter).ToListAsync();

//            var stats = new DeploymentStatistics
//            {
//                TotalDeployments = logs.Count(l => l.Status == DeploymentStatus.Success || l.Status == DeploymentStatus.Failed),
//                SuccessfulDeployments = logs.Count(l => l.Status == DeploymentStatus.Success),
//                FailedDeployments = logs.Count(l => l.Status == DeploymentStatus.Failed),
//                TotalErrors = logs.Count(l => l.Level == LogLevel.Error),
//                TotalWarnings = logs.Count(l => l.Level == LogLevel.Warning),
//                AverageDurationMs = logs.Where(l => l.DurationMs > 0).Any()
//                    ? (long)logs.Where(l => l.DurationMs > 0).Average(l => l.DurationMs)
//                    : 0,
//                PlatformBreakdown = logs
//                    .Where(l => !string.IsNullOrEmpty(l.Platform))
//                    .GroupBy(l => l.Platform)
//                    .ToDictionary(g => g.Key, g => g.Count()),
//                RecentDeployments = logs
//                    .Where(l => l.Status == DeploymentStatus.Success || l.Status == DeploymentStatus.Failed)
//                    .OrderByDescending(l => l.Timestamp)
//                    .Take(10)
//                    .ToList()
//            };

//            if (stats.TotalDeployments > 0)
//            {
//                stats.SuccessRate = (double)stats.SuccessfulDeployments / stats.TotalDeployments * 100;
//            }

//            return stats;
//        }
//    }

//    public class DeploymentStatistics
//    {
//        public int TotalDeployments { get; set; }
//        public int SuccessfulDeployments { get; set; }
//        public int FailedDeployments { get; set; }
//        public double SuccessRate { get; set; }
//        public int TotalErrors { get; set; }
//        public int TotalWarnings { get; set; }
//        public long AverageDurationMs { get; set; }
//        public Dictionary<string, int> PlatformBreakdown { get; set; }
//        public List<LogEntry> RecentDeployments { get; set; }
//    }
//}