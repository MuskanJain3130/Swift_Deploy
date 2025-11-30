using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SwiftDeploy.Models;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DeploymentsController : ControllerBase
    {
        private readonly IMongoCollection<Deployment> _deployments;

        public DeploymentsController(IMongoDatabase mongoDatabase)
        {
            _deployments = mongoDatabase.GetCollection<Deployment>("Deployments");
        }

        [HttpPost]
        public IActionResult CreateDeployment([FromBody] Dictionary<string, object> body)
        {
            if (body == null)
                return BadRequest("Deployment data is required");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var deployment = new Deployment
            {
                UserId = body.ContainsKey("userId") ? body["userId"].ToString() : userId,
                Platform = body.ContainsKey("platform") ? body["platform"].ToString() : null,
                RepoId = body.ContainsKey("repoId") ? body["repoId"].ToString() : null,
                GitHubRepoUrl = body.ContainsKey("gitHubRepoUrl") ? body["gitHubRepoUrl"].ToString() : null,
                ServiceId = body.ContainsKey("serviceId") ? body["serviceId"].ToString() : null,
                ServiceUrl = body.ContainsKey("serviceUrl") ? body["serviceUrl"].ToString() : null,
                ConfigFileUrl = body.ContainsKey("configFileUrl") ? body["configFileUrl"].ToString() : null,
                Status = "queued",
                DeployedAt = DateTime.UtcNow
            };

            _deployments.InsertOne(deployment);

            return CreatedAtAction(nameof(GetDeploymentById), new { id = deployment.Id }, deployment);
        }

        [HttpGet]
        public IActionResult GetAllDeployments()
        {
            // Get current user's deployments
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            var list = _deployments.Find(d => d.UserId == userId)
                .SortByDescending(d => d.DeployedAt)
                .ToList();
            return Ok(list);
        }

        [HttpGet("user/{userId}")]
        public IActionResult GetDeploymentsByUserId(string userId)
        {
            var list = _deployments.Find(d => d.UserId == userId)
                .SortByDescending(d => d.DeployedAt)
                .ToList();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public IActionResult GetDeploymentById(string id)
        {
            var deployment = _deployments.Find(d => d.Id == id).FirstOrDefault();
            if (deployment == null)
                return NotFound();

            return Ok(deployment);
        }

        [HttpPut("{id}/status")]
        public IActionResult UpdateDeploymentStatus(string id, [FromBody] Dictionary<string, string> body)
        {
            if (body == null || !body.ContainsKey("status"))
                return BadRequest("Status is required");

            var update = Builders<Deployment>.Update.Set(d => d.Status, body["status"]);
            var result = _deployments.UpdateOne(d => d.Id == id, update);
            if (result.ModifiedCount == 0)
                return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteDeployment(string id)
        {
            var result = _deployments.DeleteOne(d => d.Id == id);

            if (result.DeletedCount == 0)
                return NotFound();

            return NoContent();
        }
    }
}

