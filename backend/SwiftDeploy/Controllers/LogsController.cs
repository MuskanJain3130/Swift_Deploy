using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Abstractions;
using MongoDB.Driver;
using SwiftDeploy.Services;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly MongoDbService _mongo;

        public LogsController(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        [HttpPost]
        public IActionResult AddLog([FromBody] LogEntry log)
        {
            _mongo.Logs.InsertOne(log);
            return Ok("Log added");
        }

        [HttpGet("{deploymentId}")]
        public async Task<IActionResult> GetDeploymentLogs(string deploymentId)
        {
            var filter = Builders<LogEntry>.Filter.Eq("DeploymentId", deploymentId);
            var cursor = await _mongo.Logs.FindAsync(filter);
            var logs = await cursor.ToListAsync();
            return Ok(logs);
        }
    }

}

