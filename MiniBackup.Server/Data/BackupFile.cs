namespace MiniBackup.Server.Data;

public class BackupFile
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public BackupSession? Session { get; set; }
}