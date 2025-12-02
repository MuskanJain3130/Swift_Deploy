using System.ComponentModel.DataAnnotations;

namespace SwiftDeploy.Models
{

    public class UploadProjectRequest
    {
        [Required]
        public string ProjectName { get; set; }
        public string RepoName { get; set; }

        public string Description { get; set; }

        [Required]
        public string Platform { get; set; }

        // ⭐ Changed: Now accepts either IFormFile OR Azure blob name
        //public IFormFile ProjectZip { get; set; }  // Optional now

        //public string AzureBlobName { get; set; }  // ⭐ NEW: Azure blob name

        [Required]
        public CommonConfig Config { get; set; }
    }

}