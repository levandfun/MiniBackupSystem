namespace MiniBackup.Agent.Caching;

public class FileCacheRecord
{
    public long SizeBytes { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string Hash { get; set; } = string.Empty;
}