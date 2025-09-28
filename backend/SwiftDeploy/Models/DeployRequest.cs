namespace SwiftDeploy.Models
{
    public class DeployRequest
    {
        public string Repo { get; set; } = string.Empty; // e.g. "username/repo"
        public string Branch { get; set; } = "main";
    }
}
