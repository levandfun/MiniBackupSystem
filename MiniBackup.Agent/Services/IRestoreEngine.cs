
namespace MiniBackup.Agent.Services;

public interface IRestoreEngine
{
    Task RunRestoreAsync(string serverUrl, int sessionId, string targetDirectory, CancellationToken token, int maxConcurrency);
}


