using MiniBackup.Shared.Models;

namespace MiniBackup.Server.Models;

public class ManifestRequest
{
    public int SessionId { get; set; }
    public BackupJobConfig Config { get; set; } = new();
    public List<FileMetadata> Files { get; set; } = new();
}