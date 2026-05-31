using MiniBackup.Shared.Models;

namespace MiniBackup.Agent.Services;

public interface INetworkClient
{
    Task<ManifestResponse?> SendManifestAsync(BackupJobConfig config, IEnumerable<FileMetadata> files, CancellationToken token);
    Task<bool> UploadFileAsync(BackupJobConfig config, FileMetadata file, int sessionId, CancellationToken token);
    Task<bool> FinishBackupAsync(BackupJobConfig config, int sessionId, CancellationToken token);
    Task<List<RestoreFileManifest>?> GetRestoreManifestAsync(string serverUrl, int sessionId, CancellationToken token);
    Task<bool> DownloadBlobAsync(string serverUrl, string hash, string destinationPath, CancellationToken token);
    Task<List<BackupSessionDto>?> GetSessionsAsync(string serverUrl, string clientName, CancellationToken token);
}