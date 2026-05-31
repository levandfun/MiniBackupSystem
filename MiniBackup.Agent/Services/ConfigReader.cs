using MiniBackup.Shared.Models;
using System.Text.Json;

namespace MiniBackup.Agent.Services;

public class JsonConfigReader : IConfigReader
{
    public async Task<BackupJobConfig> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<BackupJobConfig>(json)
               ?? throw new InvalidOperationException("Invalid config");
    }

    public async Task SaveAsync(BackupJobConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}