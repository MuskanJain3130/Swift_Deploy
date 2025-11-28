////using Microsoft.AspNetCore.Http;
////using Microsoft.AspNetCore.Mvc;
////using MongoDB.Driver;
////using SwiftDeploy.Services;
////using SwiftDeploy.Models;
////using Microsoft.AspNetCore.Authorization; // <-- Add this using directive

////namespace SwiftDeploy.Controllers
////{
////    [Route("api/[controller]")]
////    [ApiController]
////    [Authorize] 
////    public class LogsController : ControllerBase
////    {
////        private readonly MongoDbService _mongo;

////        public LogsController(MongoDbService mongo)
////        {
////            _mongo = mongo;
////        }

////        [HttpPost]
////        public IActionResult AddLog([FromBody] LogEntry log) // <-- Use SwiftDeploy.Models.LogEntry
////        {
////            if (log == null)
////            {
////                return BadRequest("Log data is required");
////            }
////            _mongo.Logs.InsertOne(log);
////            return Ok("Log added");
////        }

////        [HttpGet("{deploymentId}")]
////        public async Task<IActionResult> GetDeploymentLogs(string deploymentId)
////        {
////            var filter = Builders<LogEntry>.Filter.Eq("DeploymentId", deploymentId); // <-- Use SwiftDeploy.Models.LogEntry
////            var cursor = await _mongo.Logs.FindAsync(filter, null);
////            var logs = await cursor.ToListAsync();
////            return Ok(logs);
////        }
////    }
////}


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using SwiftDeploy.Models;
//using SwiftDeploy.Services;
//using SwiftDeploy.Services.Interfaces;
//using LogLevel = SwiftDeploy.Models.LogLevel;

//namespace SwiftDeploy.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class LogsController : ControllerBase
//    {
//        private readonly ILoggingService _loggingService;

//        public LogsController(ILoggingService loggingService)
//        {
//            _loggingService = loggingService;
//        }

//        /// <summary>
//        /// Get logs by deployment ID
//        /// </summary>
//        [HttpGet("deployment/{deploymentId}")]
//        public async Task<IActionResult> GetLogsByDeployment(string deploymentId)
//        {
//            try
//            {
//                var logs = await _loggingService.GetLogsByDeploymentIdAsync(deploymentId);
//                return Ok(new
//                {
//                    success = true,
//                    count = logs.Count,
//                    logs = logs
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    error = $"Failed to retrieve logs: {ex.Message}"
//                });
//            }
//        }

//        /// <summary>
//        /// Get logs by repository ID
//        /// </summary>
//        [HttpGet("repository/{repositoryId}")]
//        public async Task<IActionResult> GetLogsByRepository(string repositoryId)
//        {
//            try
//            {
//                var logs = await _loggingService.GetLogsByRepositoryIdAsync(repositoryId);
//                return Ok(new
//                {
//                    success = true,
//                    count = logs.Count,
//                    logs = logs
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    error = $"Failed to retrieve logs: {ex.Message}"
//                });
//            }
//        }

//        /// <summary>
//        /// Get logs by user ID
//        /// </summary>
//        [HttpGet("user/{userId}")]
//        public async Task<IActionResult> GetLogsByUser(string userId)
//        {
//            try
//            {
//                var logs = await _loggingService.GetLogsByUserIdAsync(userId);
//                return Ok(new
//                {
//                    success = true,
//                    count = logs.Count,
//                    logs = logs
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    error = $"Failed to retrieve logs: {ex.Message}"
//                });
//            }
//        }

//        /// <summary>
//        /// Get logs by user and repository
//        /// </summary>
//        [HttpGet("user/{userId}/repository/{repositoryId}")]
//        public async Task<IActionResult> GetLogsByUserAndRepository(string userId, string repositoryId)
//        {
//            try
//            {
//                var logs = await _loggingService.GetLogsByUserAndRepositoryAsync(userId, repositoryId);
//                return Ok(new
//                {
//                    success = true,
//                    count = logs.Count,
//                    logs = logs
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    error = $"Failed to retrieve logs: {ex.Message}"
//                });
//            }
//        }

//        /// <summary>
//        /// Get deployment statistics for a user
//        /// </summary>
//        [HttpGet("statistics/user/{userId}")]
//        public async Task<IActionResult> GetUserStatistics(string userId, [FromQuery] string repositoryId = null)
//        {
//            try
//            {
//                var stats = await _loggingService.GetDeploymentStatisticsAsync(userId, repositoryId);
//                return Ok(new
//                {
//                    success = true,
//                    statistics = stats
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    error = $"Failed to retrieve statistics: {ex.Message}"
//                });
//            }
//        }

//        /// <summary>
//        /// Search logs with filters
//        /// </summary>
//        [HttpPost("search")]
//        public async Task<IActionResult> SearchLogs([FromBody] LogSearchRequest request)
//        {
//            try
//            {
//                var logs = await _loggingService.GetLogsByUserIdAsync(request.UserId);

//                if (!string.IsNullOrEmpty(request.RepositoryId))
//                {
//                    logs = logs.Where(l => l.RepositoryId == request.RepositoryId).ToList();
//                }

//                if (!string.IsNullOrEmpty(request.DeploymentId))
//                {
//                    logs = logs.Where(l => l.DeploymentId == request.DeploymentId).ToList();
//                }

//                if (request.Level.HasValue)
//                {
//                    logs = logs.Where(l => l.Level == request.Level.Value).ToList();
//                }

//                if (request.Status.HasValue)
//                {
//                    logs = logs.Where(l => l.Status == request.Status.Value).ToList();
//                }

//                if (!string.IsNullOrEmpty(request.Platform))
//                {
//                    logs = logs.Where(l => l.Platform == request.Platform).ToList();
//                }

//                if (request.StartDate.HasValue)
//                {
//                    logs = logs.Where(l => l.Timestamp >= request.StartDate.Value).ToList();
//                }

//                if (request.EndDate.HasValue)
//                {
//                    logs = logs.Where(l => l.Timestamp <= request.EndDate.Value).ToList();
//                }

//                return Ok(new
//                {
//                    success = true,
//                    count = logs.Count,
//                    logs = logs.OrderByDescending(l => l.Timestamp).ToList()
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new
//                {
//                    success = false,
//                    error = $"Failed to search logs: {ex.Message}"
//                });
//            }
//        }
//    }

//    public class LogSearchRequest
//    {
//        public string UserId { get; set; }
//        public string RepositoryId { get; set; }
//        public string DeploymentId { get; set; }
//        public LogLevel? Level { get; set; }
//        public DeploymentStatus? Status { get; set; }
//        public string Platform { get; set; }
//        public DateTime? StartDate { get; set; }
//        public DateTime? EndDate { get; set; }
//    }
//}

