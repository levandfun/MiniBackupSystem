
namespace MiniBackup.Shared.Models;

public class RestoreFileManifest
{
    public string Hash { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}
