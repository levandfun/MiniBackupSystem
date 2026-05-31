namespace MiniBackup.Agent.Caching;

public interface ILocalStateCache
{
    void Initialize(string jobId);
    string? GetCachedHash(string fullPath, long currentSize, DateTime currentModifiedUtc);
    void UpdateCache(string fullPath, long size, DateTime modifiedUtc, string hash);
    Task SaveToFileAsync();
}
