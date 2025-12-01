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
        private readonly IMongoCollection<Project> _projects;

        public DeploymentsController(IMongoDatabase mongoDatabase)
            {
            _deployments = mongoDatabase.GetCollection<Deployment>("Deployments");
            _projects = mongoDatabase.GetCollection<Project>("Projects");
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
            [HttpPost("repo")]
            public IActionResult GetDeploymentByUserId([FromBody] Dictionary<string, string> request)
            {
                if (request == null || !request.TryGetValue("repoId", out var repoId))
                    return BadRequest("repoId is required in the request body");

                var deployment = _deployments.Find(d => d.RepoId == repoId).FirstOrDefault();
                if (deployment == null)
                    return NotFound();

                return Ok(deployment);
            }


        //[HttpGet]
        // public IActionResult GetAllDeployments()
        // {
        //     var list = _deployments.Find(_ => true).ToList();
        //     return Ok(list);
        // }

        [HttpGet("user/{id}")]
        public IActionResult GetDeploymentByUserId(string id)
        {
            var deployments 
             = _deployments.Find(d => d.UserId == id).ToList();
            
            return Ok(deployments);

        }

        [HttpGet("{id}")]
        public IActionResult GetDeploymentById(string id)
        {
            var deployments = _deployments.Find(d => d.Id == id).FirstOrDefault();
            return Ok(deployments);
        }

        [HttpPost("latest")]
        public IActionResult GetLatestDeploymentById([FromBody] string repoId)
        {
            var deployments = _deployments.Find(d => d.RepoId == repoId).SortByDescending(d=>d.DeployedAt).FirstOrDefault();
            return Ok(deployments);
        }

        [HttpPut("{id}/status")]
            public IActionResult UpdateDeploymentStatus(string id, [FromBody] string status)
            {
                var update = Builders<Deployment>.Update.Set(d => d.Status, status);
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

