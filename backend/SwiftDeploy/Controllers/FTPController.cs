using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SwiftDeploy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class FTPController : ControllerBase
    {
        private readonly string basePath = Path.Combine(Directory.GetCurrentDirectory(), "FtpStorage");

        public FTPController()
        {
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
    }
}