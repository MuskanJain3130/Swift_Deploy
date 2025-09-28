using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SwiftDeploy.Models;
using System.Net.Http.Headers;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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

                var deployment = new Deployment
                {
                    RepoId = body.ContainsKey("repoId") ? body["repoId"].ToString() : null,
                    ServiceId = body.ContainsKey("serviceId") ? body["serviceId"].ToString() : null,
                    ServiceUrl = body.ContainsKey("serviceUrl") ? body["serviceUrl"].ToString() : null,
                    Status = "queued", // default
                    DeployedAt = DateTime.UtcNow
                };

                _deployments.InsertOne(deployment);

                return CreatedAtAction(nameof(GetDeploymentById), new { id = deployment.Id }, deployment);
            }

            [HttpGet]
            public IActionResult GetAllDeployments()
            {
                var list = _deployments.Find(_ => true).ToList();
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

