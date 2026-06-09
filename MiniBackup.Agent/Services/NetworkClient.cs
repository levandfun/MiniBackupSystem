using Microsoft.Extensions.Logging;
using MiniBackup.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniBackup.Agent.Services;

public class NetworkClient(HttpClient httpClient, ILogger<NetworkClient> logger) : INetworkClient
{
    private readonly ILogger<NetworkClient> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;

    public async Task<int> CreateSessionAsync(BackupJobConfig config, CancellationToken token)
    {
        var url = $"{config.ServerUrl.TrimEnd('/')}/api/backup/session/create";
        var requestData = new CreateSessionRequest { ClientName = config.ClientName };

        var response = await _httpClient.PostAsJsonAsync(url, requestData, token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
        return result.GetProperty("sessionId").GetInt32();
    }

    public async Task<ManifestResponse?> SendManifestAsync(BackupJobConfig config, int sessionId,  IEnumerable<FileMetadata> files, CancellationToken token)
    {
        var url = $"{config.ServerUrl.TrimEnd('/')}/api/backup/start";
        var requestData = new 
        {
            SessionId = sessionId,
            Config = config,
            Files = files.Select(f => new FileMetadata
            {
                FilePath = f.FilePath, 
                RelativePath = f.RelativePath,
                Hash = f.Hash,
                Size = f.Size
            }).ToList()
        };
        try
        {
            _logger.LogInformation("Sending manifest to {Endpoint} for job {JobId} with {FileCount} files", url, config.JobId, files.Count());
            var response = await _httpClient.PostAsJsonAsync(url, requestData, token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ManifestResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                token);
                _logger.LogInformation("Manifest sent successfully for job {JobId}. Server responded with {FileCount} files to upload.", config.JobId, result?.FilesToUpload.Count ?? 0);
                return result;
            }
            else
            {
                _logger.LogError("Failed to send manifest for job {JobId}. Status code: {StatusCode}", config.JobId, response.StatusCode);
                return null;
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending manifest for job {JobId}", config.JobId);
            return null;
        }
    }

    public async Task<bool> UploadFileAsync(BackupJobConfig config, FileMetadata file, int sessionId, CancellationToken token)
    {
        var targetEndpoint = $"{config.ServerUrl.TrimEnd('/')}/api/backup/upload";
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(File.OpenRead(file.FilePath));
            content.Add(new StringContent(file.FilePath), "originalPath");
            content.Add(streamContent, "file", file.Name);
            content.Add(new StringContent(file.RelativePath), "relativePath");
            content.Add(new StringContent(file.Hash), "hash");
            content.Add(new StringContent(sessionId.ToString()), "sessionId");
            _logger.LogInformation("Uploading file {FilePath} to {Endpoint} for job {JobId}", file.FilePath, targetEndpoint, config.JobId);
            var response = await _httpClient.PostAsync(targetEndpoint, content, token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("File {FilePath} uploaded successfully for job {JobId}", file.FilePath, config.JobId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to upload file {FilePath} for job {JobId}. Status code: {StatusCode}", file.FilePath, config.JobId, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FilePath}", file.FilePath);
            return false;
        }
    }

    public async Task<bool> FinishBackupAsync(BackupJobConfig config, int sessionId, CancellationToken token)
    {
        var url = $"{config.ServerUrl.TrimEnd('/')}/api/backup/finish?sessionId={sessionId}";
        try
        {
            _logger.LogInformation("Sending backup finish signal to {Endpoint} for job {JobId}", url, config.JobId);
            var response = await _httpClient.PostAsync(url, null, token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Backup finish signal sent successfully for job {JobId}", config.JobId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to send backup finish signal for job {JobId}. Status code: {StatusCode}", config.JobId, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending backup finish signal for job {JobId}", config.JobId);
            return false;
        }
    }

    public async Task<List<RestoreFileManifest>?> GetRestoreManifestAsync(string serverUrl, int sessionId, CancellationToken token)
    {
        var url = $"{serverUrl.TrimEnd('/')}/api/restore/{sessionId}/manifest";
        try
        {
            _logger.LogInformation("Fetching manifest for session {SessionId}...", sessionId);
            var manifest = await _httpClient.GetFromJsonAsync<List<RestoreFileManifest>>(url, cancellationToken: token);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching manifest for session {SessionId}", sessionId);
            return null;
        }
    }
    public async Task<bool> DownloadBlobAsync(string serverUrl, string hash, string destinationPath, CancellationToken token)
    {
        var url = $"{serverUrl.TrimEnd('/')}/api/restore/blob/{hash}";
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream, token);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while downloading blob {Hash}", hash);
            return false;
        }
    }

    public async Task<List<BackupSessionDto>?> GetSessionsAsync(string serverUrl,string clientName, CancellationToken token)
    {
        try
        {
            var url = $"{serverUrl.TrimEnd('/')}/api/backup/sessions?clientName={Uri.EscapeDataString(clientName)}";
            return await _httpClient.GetFromJsonAsync<List<BackupSessionDto>>(url, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve list of backups from server.");
            return null;
        }
    }
}
