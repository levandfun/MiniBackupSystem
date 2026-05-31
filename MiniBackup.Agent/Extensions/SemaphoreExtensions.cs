namespace MiniBackup.Agent.Extensions;

public readonly struct SemaphoreReleaser(SemaphoreSlim semaphore) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = semaphore;

    public void Dispose()
    {
        _semaphore.Release();
    }
}

public static class SemaphoreExtensions
{
    public static async Task<IDisposable> UseWaitAsync(this SemaphoreSlim semaphore, CancellationToken token = default)
    {
        await semaphore.WaitAsync(token).ConfigureAwait(false);

        return new SemaphoreReleaser(semaphore);
    }
}