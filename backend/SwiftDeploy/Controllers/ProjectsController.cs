using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SwiftDeploy.Models;
using System.Collections.Generic;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IMongoCollection<Project> _Projects;

        public ProjectsController(IMongoDatabase mongoDatabase)
        {
            _Projects = mongoDatabase.GetCollection<Project>("Projects");
        }

        [HttpPost]
        public IActionResult CreateProject([FromBody] Dictionary<string, object> body)
        {
            if (body == null)
                return BadRequest("Project data is required");

            var project = new Project
            {
                UserId = body.ContainsKey("userId") ? body["userId"]?.ToString() : null,
                ProjectName = body.ContainsKey("projectName") ? body["projectName"]?.ToString() : null,
                RepoId = body.ContainsKey("repoId") ? body["repoId"]?.ToString() : null,
                Branch = body.ContainsKey("branch") ? body["branch"]?.ToString() : null,
                CreatedAt = DateTime.UtcNow
            };

            _Projects.InsertOne(project);

            return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
        }

        [HttpGet]
        public IActionResult GetAllProjects()
        {
            var list = _Projects.Find(_ => true).ToList();
            return Ok(list);
        }

        //[HttpGet("{name}")]
        //public IActionResult GetProjectByName(string name)
        //{
        //    var project = _Projects.Find(d => d.ProjectName == name).FirstOrDefault();
        //    if (project == null)
        //        return NotFound();

        //    return Ok(project);
        //}
        [HttpGet("user/{id}")]
        public IActionResult GetProjectByUserId(string id)
        {
            var project = _Projects.Find(d => d.UserId == id).ToList();
            if (project == null)
                return NotFound();

            return Ok(project);
        }
        [HttpGet("{id}")]
        public IActionResult GetProjectById(string id)
        {
            var project = _Projects.Find(d => d.Id == id).FirstOrDefault();
            if (project == null)
                return NotFound();

            return Ok(project);
        }

        [HttpPut("{id}")]
        public IActionResult UpdateProject(string id, [FromBody] Dictionary<string, object> body)
        {
            var updateDef = Builders<Project>.Update;
            var updates = new List<UpdateDefinition<Project>>();

            if (body.ContainsKey("userId")) updates.Add(updateDef.Set(d => d.UserId, body["userId"]?.ToString()));
            if (body.ContainsKey("projectName")) updates.Add(updateDef.Set(d => d.ProjectName, body["projectName"]?.ToString()));
            if (body.ContainsKey("repoId")) updates.Add(updateDef.Set(d => d.RepoId, body["repoId"]?.ToString()));
            if (body.ContainsKey("branch")) updates.Add(updateDef.Set(d => d.Branch, body["branch"]?.ToString()));

            if (updates.Count == 0) return BadRequest("No fields to update");

            var combinedUpdate = updateDef.Combine(updates);
            var result = _Projects.UpdateOne(d => d.Id == id, combinedUpdate);
            if (result.ModifiedCount == 0)
                return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteProject(string id)
        {
            var result = _Projects.DeleteOne(d => d.ProjectName == id);
            if (result.DeletedCount == 0)
                return NotFound();

            return NoContent();
        }
    }
}
