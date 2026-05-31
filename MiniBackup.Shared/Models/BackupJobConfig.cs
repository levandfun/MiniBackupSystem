using System.Text.Json.Serialization;

namespace MiniBackup.Shared.Models;

public class BackupJobConfig
{
    [JsonPropertyName("job_id")]
    public Guid JobId { get; set; }

    [JsonPropertyName("job_name")]
    public string JobName { get; set; } = string.Empty;

    [JsonPropertyName("client_name")]
    public string ClientName { get; set; } = Environment.MachineName;

    [JsonPropertyName("source_directories")]
    public List<string> SourceDirectories { get; set; } = new();

    [JsonPropertyName("backup_type")]
    public string BackupType { get; set; } = "Full";
    [JsonPropertyName("server_url")]
    public string ServerUrl { get; set; } = "http://localhost:5000";
    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1000;
    public int MaxConcurrency { get; set; } = 4;
}