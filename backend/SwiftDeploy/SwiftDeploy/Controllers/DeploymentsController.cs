using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SwiftDeploy.Models;

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

        /// <summary>
        /// Create a new deployment record (and optionally trigger Render API)
        /// </summary>
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


        /// <summary>
        /// Get all deployments
        /// </summary>
        [HttpGet]
            public IActionResult GetAllDeployments()
            {
                var list = _deployments.Find(_ => true).ToList();
                return Ok(list);
            }

            /// <summary>
            /// Get a single deployment by ID
            /// </summary>
            [HttpGet("{id}")]
            public IActionResult GetDeploymentById(string id)
            {
                var deployment = _deployments.Find(d => d.Id == id).FirstOrDefault();
                if (deployment == null)
                    return NotFound();

                return Ok(deployment);
            }

            /// <summary>
            /// Update a deployment's status
            /// </summary>
            [HttpPut("{id}/status")]
            public IActionResult UpdateDeploymentStatus(string id, [FromBody] string status)
            {
                var update = Builders<Deployment>.Update.Set(d => d.Status, status);
                var result = _deployments.UpdateOne(d => d.Id == id, update);

                if (result.ModifiedCount == 0)
                    return NotFound();

                return NoContent();
            }

            /// <summary>
            /// Delete a deployment
            /// </summary>
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

