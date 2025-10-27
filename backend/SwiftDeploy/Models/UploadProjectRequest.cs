using System.ComponentModel.DataAnnotations;

namespace SwiftDeploy.Models
{
    public class UploadProjectRequest
    {

        
        
        [Required]
        public string ProjectName { get; set; }

        public string Description { get; set; }

        [Required]
        public string Platform { get; set; }

        [Required]
        public IFormFile ProjectZip { get; set; }

        [Required]
        public CommonConfig Config { get; set; }
    }
}