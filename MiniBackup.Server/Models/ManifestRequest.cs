using MiniBackup.Shared.Models;

namespace MiniBackup.Server.Models;

public class ManifestRequest
{
    public BackupJobConfig Config { get; set; } = new();
    public List<FileMetadata> Files { get; set; } = new();
    public string ClientName { get; set; } = string.Empty; 
}