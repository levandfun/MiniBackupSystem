using MiniBackup.Shared.Models;

namespace MiniBackup.Agent.Services;

public interface IFileScanner
{
    IAsyncEnumerable<FileMetadata> ScanAsync(string directoryPath, CancellationToken cancellationToken);

}
