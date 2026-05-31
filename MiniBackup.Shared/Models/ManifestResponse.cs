namespace MiniBackup.Shared.Models;

public class ManifestResponse
{
    public int SessionId { get; set; }
    public List<string> FilesToUpload { get; set; } = new();
}