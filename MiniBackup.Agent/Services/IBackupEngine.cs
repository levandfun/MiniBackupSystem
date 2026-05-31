namespace MiniBackup.Agent.Services;

public interface IBackupEngine
{
    Task RunAsync(string configPath, CancellationToken cancellationToken);
}