namespace MiniBackup.Agent.Services;

using MiniBackup.Shared.Models;

public interface IBackupEngine
{
    Task RunAsync(BackupJobConfig config, CancellationToken cancellationToken);
}