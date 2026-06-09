using Microsoft.Extensions.Logging;
using MiniBackup.Agent.Extensions;
using MiniBackup.Shared.Models;
using System.Security.Cryptography;

namespace MiniBackup.Agent.Services;

public class RestoreEngine(INetworkClient networkClient, ILogger<RestoreEngine> logger) : IRestoreEngine
{
    private readonly INetworkClient _networkClient = networkClient;
    private readonly ILogger<RestoreEngine> _logger = logger;

    public async Task RunRestoreAsync(string serverUrl, int sessionId, string targetDirectory, CancellationToken token, int maxConcurrency)
    {
        _logger.LogInformation("Beginning restore for session {SessionId} to directory {TargetDir}", sessionId, targetDirectory);

        var manifest = await _networkClient.GetRestoreManifestAsync(serverUrl, sessionId, token);
        if (manifest == null || manifest.Count == 0)
        {
            _logger.LogWarning("Manifest is empty or unavailable. Cancelling.");
            return;
        }

        _logger.LogInformation("Retrieved {Count} files in the manifest. Starting download...", manifest.Count);
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var downloadTasks = new List<Task>();

        foreach (var fileDef in manifest)
        {
            downloadTasks.Add(Task.Run(async () =>
            {
                using (await semaphore.UseWaitAsync(token))
                {
                    await ProcessSingleFileAsync(serverUrl, fileDef, targetDirectory, token);
                }
            }, token));
        }

        await Task.WhenAll(downloadTasks);
        _logger.LogInformation("Restore for session {SessionId} completed successfully!", sessionId);
    }

    private async Task ProcessSingleFileAsync(string serverUrl, RestoreFileManifest fileDef, string targetDirectory, CancellationToken token)
    {
        string finalPath = Path.Combine(targetDirectory, fileDef.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        string tempPath = finalPath + ".tmp";

        _logger.LogInformation("Downloading {Path}...", fileDef.RelativePath);

        bool success = await _networkClient.DownloadBlobAsync(serverUrl, fileDef.Hash, tempPath, token);
        if (!success) return;

        string downloadedHash;
        using (var stream = File.OpenRead(tempPath))
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = await sha256.ComputeHashAsync(stream, token);
            downloadedHash = Convert.ToHexStringLower(hashBytes);
        }

        if (downloadedHash != fileDef.Hash)
        {
            _logger.LogError("CRITICAL HASH ERROR! File {Path} is corrupted. Expected: {Expected}, Actual: {Actual}",
                fileDef.RelativePath, fileDef.Hash, downloadedHash);

            File.Delete(tempPath); 
            return; 
        }

        File.Move(tempPath, finalPath, overwrite: true);
        _logger.LogInformation("[+] Restored: {Path}", fileDef.RelativePath);
    }
}