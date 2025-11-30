using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Storage.Blobs;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using SwiftDeploy.Services.Interfaces;
using MongoDB.Driver;
using SwiftDeploy.Models;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;
using System;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FTPController : ControllerBase
    {
        private readonly string basePath = Path.Combine(Directory.GetCurrentDirectory(), "FtpStorage");
        private readonly IConfiguration _configuration;
        private readonly IUnifiedDeploymentService _deploymentService;
        private readonly IMongoCollection<Project> _projectsCollection;
        private readonly IMongoCollection<User> _usersCollection;
        private readonly ILogger<FTPController> _logger;

        public FTPController(
            IConfiguration configuration,
            IUnifiedDeploymentService deploymentService,
            IMongoDatabase mongoDatabase,
            ILogger<FTPController> logger)
        {
            _configuration = configuration;
            _deploymentService = deploymentService;
            _logger = logger;

            // initialize projects collection
            _projectsCollection = mongoDatabase.GetCollection<Project>("Projects");
            _usersCollection = mongoDatabase.GetCollection<User>("Users");

            // Ensure storage folder exists
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
        }

        // Upload a file (simulates FTP upload)
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, string remotePath = "")
        {
            if (file == null) return BadRequest("No file provided");

            var targetDir = Path.Combine(basePath, remotePath);
            Directory.CreateDirectory(targetDir);

            var filePath = Path.Combine(targetDir, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { path = filePath, message = "File uploaded successfully" });
        }

        // Download a file (simulates FTP download)
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile(string path)
        {
            var fullPath = Path.Combine(basePath, path);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found");

            var memory = new MemoryStream();
            using (var stream = new FileStream(fullPath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", Path.GetFileName(fullPath));
        }

        // List files in directory (simulates FTP LIST)
        [HttpGet("list")]
        public IActionResult ListFiles(string directory = "")
        {
            var dirPath = Path.Combine(basePath, directory);

            if (!Directory.Exists(dirPath))
                return NotFound("Directory not found");

            var files = Directory.GetFiles(dirPath);

            List<string> fileNames = new List<string>();
            foreach (var file in files)
            {
                fileNames.Add(Path.GetFileName(file));
            }

            return Ok(fileNames);
        }

        // Delete a file
        [HttpDelete("delete")]
        public IActionResult DeleteFile(string path)
        {
            var fullPath = Path.Combine(basePath, path);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found");

            System.IO.File.Delete(fullPath);
            return Ok("File deleted");
        }

        // DTO for import request
        public class ImportFromAzureRequest
        {
            public string BlobName { get; set; } = string.Empty;      // name of the blob (zip file)
            public string ProjectName { get; set; } = string.Empty;   // desired repo / project name
            public string? Description { get; set; }                  // optional description for repo
            public string? UserId { get; set; }                       // optional owner/user id to store in Projects collection
        }

        /// <summary>
        /// Download zip from Azure Blob Storage, extract, push to a new SwiftDeploy GitHub repo using IUnifiedDeploymentService,
        /// create a Project document in MongoDB, and cleanup local files.
        /// Requires configuration keys:
        ///   - Azure:ConnectionString
        ///   - Azure:ContainerName
        /// </summary>
        [HttpPost("import-from-azure")]
        public async Task<IActionResult> ImportFromAzure([FromBody] ImportFromAzureRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.BlobName) || string.IsNullOrWhiteSpace(request.ProjectName))
                return BadRequest("BlobName and ProjectName are required.");

            var connectionString = _configuration["Azure:ConnectionString"];
            var containerName = _configuration["Azure:ContainerName"];

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
                return StatusCode(500, "Azure storage configuration missing (Azure:ConnectionString or Azure:ContainerName).");

            var tmpFolder = Path.Combine(Path.GetTempPath(), "swiftdeploy_imports");
            Directory.CreateDirectory(tmpFolder);

            var localZipPath = Path.Combine(tmpFolder, request.BlobName);
            var extractDir = Path.Combine(tmpFolder, Guid.NewGuid().ToString("N"));

            try
            {
                // Download blob to local file
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(request.BlobName);

                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    _logger.LogWarning("Blob not found: {BlobName}", request.BlobName);
                    return NotFound($"Blob '{request.BlobName}' not found in container '{containerName}'.");
                }

                await blobClient.DownloadToAsync(localZipPath);

                // Ensure extraction directory
                Directory.CreateDirectory(extractDir);

                // Extract zip
                try
                {
                    ZipFile.ExtractToDirectory(localZipPath, extractDir);
                }
                catch (InvalidDataException ide)
                {
                    _logger.LogError(ide, "Failed to extract zip file: {ZipPath}", localZipPath);
                    return BadRequest("The downloaded file is not a valid zip archive.");
                }

                // Create GitHub repo under SwiftDeploy account and push code using the deployment service
                _logger.LogInformation("Creating SwiftDeploy repo for project {ProjectName}", request.ProjectName);
                var repoName = await _deploymentService.CreateSwiftDeployRepoAsync(request.ProjectName, request.Description ?? string.Empty);

                _logger.LogInformation("Pushing code from {ExtractDir} to repo {RepoName}", extractDir, repoName);
                await _deploymentService.PushCodeToSwiftDeployRepoAsync(repoName, extractDir);

                // Create Projects document in MongoDB
                var project = new Project
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    UserId = request.UserId,
                    ProjectName = request.ProjectName,
                    RepoId = repoName,
                    Branch = "main",
                    CreatedAt = DateTime.UtcNow
                };

                await _projectsCollection.InsertOneAsync(project);

                _logger.LogInformation("Imported project {ProjectName} as repo {RepoName} and persisted project id {ProjectId}", request.ProjectName, repoName, project.Id);

                // Cleanup local files
                try
                {
                    if (System.IO.File.Exists(localZipPath))
                        System.IO.File.Delete(localZipPath);

                    if (Directory.Exists(extractDir))
                        Directory.Delete(extractDir, true);
                }
                catch (Exception cleanEx)
                {
                    _logger.LogWarning(cleanEx, "Failed to fully clean up temporary files for import {ProjectName}", request.ProjectName);
                }

                return Ok(new
                {
                    success = true,
                    repoName = repoName,
                    gitHubRepoUrl = $"https://github.com/swiftdeploy-repos/{repoName}",
                    projectId = project.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing blob '{BlobName}' to GitHub for project '{ProjectName}'", request.BlobName, request.ProjectName);

                // Try to cleanup partial artifacts
                try
                {
                    if (System.IO.File.Exists(localZipPath))
                        System.IO.File.Delete(localZipPath);
                    if (Directory.Exists(extractDir))
                        Directory.Delete(extractDir, true);
                }
                catch { /* swallow cleanup errors */ }

                return StatusCode(500, new { success = false, message = $"Import failed: {ex.Message}" });
            }
        }


        // Returns Azure credentials from appsettings only when provided credentials match a user in MongoDB.
        [HttpPost("azure/creds")]
        public async Task<IActionResult> GetAzureCredentials([FromBody] AzureCredsRequest request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Username, password and email are required.");

            try
            {
                var user = await _usersCollection.Find(u =>
                    (u.Username == request.Username || u.Email == request.Email)).FirstOrDefaultAsync();

                if (user == null)
                    return Unauthorized(new { success = false, message = "Invalid credentials." });

                // Ensure the user has a password hash (oauth-only users will not)
                if (string.IsNullOrEmpty(user.PasswordHash))
                    return Unauthorized(new { success = false, message = "Invalid credentials." });

                bool verified = false;
                try
                {
                    verified = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                }
                catch
                {
                    // Do not leak details; treat as invalid
                    verified = false;
                }

                if (!verified || !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(new { success = false, message = "Invalid credentials." });
                }

                // Credentials valid — prepare Azure creds from configuration
                var azureHost = _configuration["Azure:Host"] ?? string.Empty;
                var azureUsername = _configuration["Azure:Username"] ?? string.Empty;
                var azurePwd = _configuration["Azure:Pwd"] ?? string.Empty;
                var azureConn = _configuration["Azure:ConnectionString"] ?? string.Empty;

                // Return minimal set necessary. Do not log secrets.
                return Ok(new
                {
                    success = true,
                    azure = new
                    {
                        host = azureHost,
                        username = azureUsername,
                        password = azurePwd,
                        connectionString = azureConn
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user credentials for azure/creds");
                return StatusCode(500, new { success = false, message = "Server error" });
            }
        }
    }
}   