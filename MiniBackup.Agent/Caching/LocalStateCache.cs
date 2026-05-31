using Microsoft.Extensions.Logging;
using MiniBackup.Agent.Caching;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MiniBackup.Agent.Caching;

public class LocalStateCache(ILogger<LocalStateCache> logger) : ILocalStateCache
{
    private static readonly JsonSerializerOptions _jsonOptions =
    new() { WriteIndented = true };
    private string _cacheFilePath = string.Empty;
    private readonly ILogger<LocalStateCache> _logger = logger;

    private ConcurrentDictionary<string, FileCacheRecord> _cache = new();

    public void Initialize(string jobId)
    {
        _cacheFilePath = $"cache_{jobId}.json";

        if (File.Exists(_cacheFilePath))
        {
            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, FileCacheRecord>>(json);
                if (loaded != null)
                {
                    _cache = new ConcurrentDictionary<string, FileCacheRecord>(loaded);
                    _logger.LogInformation("Local cache loaded from {File}. Known files: {Count}", _cacheFilePath, _cache.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load cache {File}, starting with a clean slate.", _cacheFilePath);
            }
        }
    }

    public string? GetCachedHash(string fullPath, long currentSize, DateTime currentModifiedUtc)
    {
        if (_cache.TryGetValue(fullPath, out var record))
        {
            var timeDiff = Math.Abs((record.LastModifiedUtc - currentModifiedUtc).TotalSeconds);

            if (record.SizeBytes == currentSize && timeDiff < 1)
            {
                return record.Hash;
            }
        }
        return null;
    }

    public void UpdateCache(string fullPath, long size, DateTime modifiedUtc, string hash)
    {
        _cache[fullPath] = new FileCacheRecord
        {
            SizeBytes = size,
            LastModifiedUtc = modifiedUtc,
            Hash = hash
        };
    }

    public async Task SaveToFileAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache, _jsonOptions);
            await File.WriteAllTextAsync(_cacheFilePath, json);
            _logger.LogInformation("Local cache successfully saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving local cache.");
        }
    }
}