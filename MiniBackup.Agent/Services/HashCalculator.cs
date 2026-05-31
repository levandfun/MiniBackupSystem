using Microsoft.Extensions.Logging;
using MiniBackup.Agent.Caching;
using MiniBackup.Shared.Models;

namespace MiniBackup.Agent.Services;

public class HashCalculator(ILocalStateCache cache, ILogger<HashCalculator> logger) : IHashCalculator
{
    private readonly ILocalStateCache _cache = cache;
    private readonly ILogger<HashCalculator> _logger = logger;

    public async Task<string> CalculateAsync(FileMetadata file, CancellationToken token)
    {
        var cachedHash = _cache.GetCachedHash(file.FilePath, file.Size, file.LastModified);

        if (cachedHash != null)
        {
            return cachedHash;
        }

        _logger.LogInformation("Calculating hash for file: {Path}", file.RelativePath);

        using var stream = File.OpenRead(file.FilePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();

        var hashBytes = await sha256.ComputeHashAsync(stream, token);
        var calculatedHash = Convert.ToHexStringLower(hashBytes);

        _cache.UpdateCache(file.FilePath, file.Size, file.LastModified, calculatedHash);

        return calculatedHash;
    }
}
