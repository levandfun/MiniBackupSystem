namespace MiniBackup.Server.Data;

public enum BackupStatus
{
    Started,
    Completed,
    Failed
}
public class BackupSession
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public BackupStatus Status { get; set; } = BackupStatus.Started;
    public string ClientName { get; set; } = string.Empty;
    public List<BackupFile> Files { get; set; } = new();
}