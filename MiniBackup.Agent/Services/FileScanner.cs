using MiniBackup.Shared.Models;
using System.Runtime.CompilerServices;

namespace MiniBackup.Agent.Services;

public class FileScanner : IFileScanner
{
    public async IAsyncEnumerable<FileMetadata> ScanAsync(string directoryPath, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(file);
            yield return new FileMetadata
            {
                FilePath = file,
                RelativePath = Path.GetRelativePath(directoryPath, fileInfo.FullName).Replace('\\', '/'),
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                Name = fileInfo.Name,
                Hash = string.Empty 
            };
        }
        await Task.Yield();
    }

}
