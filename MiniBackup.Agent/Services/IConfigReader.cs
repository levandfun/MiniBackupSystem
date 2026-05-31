using MiniBackup.Shared.Models;

namespace MiniBackup.Agent.Services;

public interface IConfigReader
{
    Task<BackupJobConfig> LoadAsync(string path);
    Task SaveAsync(BackupJobConfig config, string path);
}