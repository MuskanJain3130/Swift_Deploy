using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SwiftDeploy.Services;
using SwiftDeploy.Models;
using Microsoft.AspNetCore.Authorization; // <-- Add this using directive

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class LogsController : ControllerBase
    {
        private readonly MongoDbService _mongo;

        public LogsController(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        [HttpPost]
        public IActionResult AddLog([FromBody] LogEntry log) // <-- Use SwiftDeploy.Models.LogEntry
        {
            if (log == null)
            {
                return BadRequest("Log data is required");
            }
            _mongo.Logs.InsertOne(log);
            return Ok("Log added");
        }

        [HttpGet("{deploymentId}")]
        public async Task<IActionResult> GetDeploymentLogs(string deploymentId)
        {
            var filter = Builders<LogEntry>.Filter.Eq("DeploymentId", deploymentId); // <-- Use SwiftDeploy.Models.LogEntry
            var cursor = await _mongo.Logs.FindAsync(filter, null);
            var logs = await cursor.ToListAsync();
            return Ok(logs);
        }
    }
}

