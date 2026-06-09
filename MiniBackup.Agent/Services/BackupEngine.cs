using Microsoft.Extensions.Logging;
using MiniBackup.Agent.Caching;
using MiniBackup.Agent.Extensions;
using MiniBackup.Shared.Models;

namespace MiniBackup.Agent.Services;

public class BackupEngine(
    IFileScanner fileScanner,
    IHashCalculator hashCalculator,
    ILogger<BackupEngine> logger,
    ILocalStateCache cache,
    INetworkClient networkClient) : IBackupEngine

{
    private readonly IFileScanner _fileScanner = fileScanner;
    private readonly IHashCalculator _hashCalculator = hashCalculator;
    private readonly ILogger<BackupEngine> _logger = logger;
    private readonly INetworkClient _networkClient = networkClient;
    private readonly ILocalStateCache _cache = cache;

    public async Task RunAsync(BackupJobConfig config, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Backup Engine started successfully via DI.");
        var batch = new List<FileMetadata>();
        var batchSize = config.BatchSize;
        _cache.Initialize(config.JobId.ToString());
        int currentSessionId = await _networkClient.CreateSessionAsync(config, cancellationToken);
        _logger.LogInformation("Created session #{SessionId}", currentSessionId);


        foreach (var dir in config.SourceDirectories)
        {
            _logger.LogInformation("Configured source directory: {Directory}", dir);
            await foreach (var file in _fileScanner.ScanAsync(dir, cancellationToken))
            {
                file.Hash = await _hashCalculator.CalculateAsync(file, cancellationToken);
                batch.Add(file);
                if (batch.Count >= batchSize)
                {
                    _logger.LogInformation("Sending batch of {Count} files to the server...", batch.Count);
                    await ProcessBatchAsync(config, currentSessionId, batch, cancellationToken);
                    batch.Clear();
                }
            }
        }
        if (batch.Count > 0)
        {
            await ProcessBatchAsync(config,currentSessionId, batch, cancellationToken);
        }
        await _networkClient.FinishBackupAsync(config, currentSessionId, cancellationToken); ;
        await _cache.SaveToFileAsync();
        _logger.LogInformation("Backup Engine finished its job.");

    }

    private async Task ProcessBatchAsync(BackupJobConfig config, int sessionId, List<FileMetadata> batch, CancellationToken token)
    {
        var manifestResponse = await _networkClient.SendManifestAsync(config, sessionId, batch, token);

        if (manifestResponse == null) return;
        if (manifestResponse.SessionId > 0)
        {
            sessionId = manifestResponse.SessionId;
        }
        int maxConcurrency = config.MaxConcurrency;
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var uploadTasks = new List<Task>();

        foreach (var filePath in manifestResponse.FilesToUpload)
        {
            var fileToUpload = batch.FirstOrDefault(f => f.FilePath == filePath);
            if (fileToUpload != null)
            {

                uploadTasks.Add(Task.Run(async () =>
                {
                    using (await semaphore.UseWaitAsync(token))
                    {
                        await _networkClient.UploadFileAsync(config, fileToUpload, sessionId, token);
                    }
                }, token));
            }
        }
        await Task.WhenAll(uploadTasks);
    }
}