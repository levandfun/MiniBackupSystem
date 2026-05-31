namespace MiniBackup.Shared.Models
{
    public class FileMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
