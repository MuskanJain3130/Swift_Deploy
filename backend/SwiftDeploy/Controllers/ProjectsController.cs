using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Octokit;
using SwiftDeploy.Models;
using System.Collections.Generic;
using Project = SwiftDeploy.Models.Project;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IMongoCollection<Project> _Projects;
        private readonly IUnifiedDeploymentService _deploymentService;

        public ProjectsController(IMongoDatabase mongoDatabase, IUnifiedDeploymentService deploymentService)
        {
            _Projects = mongoDatabase.GetCollection<Project>("Projects");
            _deploymentService = deploymentService;
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

        [HttpPost("new-without-github-project")]
        public async Task<IActionResult> CreateWithoutGithubProject([FromBody] Dictionary<string, object> body)
        {
            if (body == null)
                return BadRequest("Project data is required");


            string projectName = body.ContainsKey("projectName") ? body["projectName"]?.ToString() : null;

            string bloburl = body.ContainsKey("bloburl") ? body["bloburl"]?.ToString() : null;
            var localProjectPath = await _deploymentService.DownloadAndExtractFromAzureAsync(bloburl,projectName);
            
            var repoName = await _deploymentService.CreateSwiftDeployRepoAsync(projectName, "generated from swift deploy");
            var GitHubRepoName = repoName;

            await _deploymentService.PushCodeToSwiftDeployRepoAsync(repoName, localProjectPath);

            // ⭐ Cleanup: Delete extracted folder after pushing to GitHub
            try
            {
                if (Directory.Exists(localProjectPath))
                {
                    Directory.Delete(localProjectPath, true);
                }
            }
            catch (Exception cleanupEx)
            {
            }

            Models.Project project = new Models.Project
            {
                UserId = body.ContainsKey("userId") ? body["userId"]?.ToString() : null,
                ProjectName = projectName,
                RepoId =  "swiftdeployapp/"+repoName,
                Branch = "main",
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
