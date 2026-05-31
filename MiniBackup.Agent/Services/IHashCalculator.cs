using MiniBackup.Shared.Models;

namespace MiniBackup.Agent.Services;

public interface IHashCalculator
{
    Task<string> CalculateAsync(FileMetadata file, CancellationToken token);
}
