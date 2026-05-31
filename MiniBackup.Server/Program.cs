using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBackup.Server.Data;
using MiniBackup.Server.Models;
using MiniBackup.Shared.Models;
using Serilog;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/server_log_.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
var serverConfig = new ServerConfig();
builder.Configuration.GetSection("ServerConfig").Bind(serverConfig);
builder.Services.AddSingleton(serverConfig);
builder.Services.AddDbContext<BackupDbContext>(options =>
    options.UseSqlite("Data Source=minibackup.db"));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
    db.Database.EnsureCreated();
}
app.MapGet("/ping", () => Results.Ok("MiniBackup Server is running!"));

app.MapPost("/api/backup/start", async (ManifestRequest request, BackupDbContext db, ILogger<Program> logger) =>
{
    var session = new BackupSession
    {
        StartedAtUtc = DateTime.UtcNow,
        Status = BackupStatus.Started,
        ClientName = request.ClientName,
    };
    db.Sessions.Add(session);
    await db.SaveChangesAsync();
    var response = new ManifestResponse { SessionId = session.Id };

    var incomingHashes = request.Files.Select(f => f.Hash).ToHashSet();
    var knownHashes = new HashSet<string>();

    foreach (var chunk in incomingHashes.Chunk(1000))
    {
        var found = await db.Files
            .Where(f => chunk.Contains(f.Hash))
            .Select(f => f.Hash)
            .ToHashSetAsync();

        knownHashes.UnionWith(found);
    }

    foreach (var file in request.Files)
    {
        bool isFileSafeAndSound = false;

        if (knownHashes.Contains(file.Hash))
        {

            string shardFolder = file.Hash[..2];
            string shardFileName = file.Hash[2..];
            string physicalPath = Path.Combine(serverConfig.StoragePath, "blobs", shardFolder, shardFileName);

            if (System.IO.File.Exists(physicalPath))
            {
                isFileSafeAndSound = true;
            }
            else
            {

                logger.LogWarning("Hash {Hash} is known but file is missing on disk. Marking for re-upload.", file.Hash);
            }
        }

        if (isFileSafeAndSound)
        {
            var linkedFile = new BackupFile
            {
                SessionId = session.Id,
                Hash = file.Hash,
                RelativePath = file.RelativePath,
                SizeBytes = file.Size
            };
            db.Files.Add(linkedFile);
        }
        else
        {
            response.FilesToUpload.Add(file.FilePath);
        }
    }
    await db.SaveChangesAsync();
    logger.LogInformation("Received manifest for job {JobId} for {ClientName} with {FileCount} files. {FilesToUploadCount} need to be uploaded.", request.Config.JobId, request.ClientName, request.Files.Count, response.FilesToUpload.Count);

    return Results.Ok(response);
});

app.MapPost("/api/backup/upload", async (
    IFormFile file,
    [FromForm] string hash,
    [FromForm] string relativePath,
    [FromForm] int sessionId,
    BackupDbContext db,
    ServerConfig config,
    ILogger<Program> logger) =>
{
    string shardFolder = hash[..2];
    string shardFileName = hash[2..];
    string blobsDirectory = Path.Combine(config.StoragePath, "blobs", shardFolder);
    Directory.CreateDirectory(blobsDirectory);

    string physicalPath = Path.Combine(blobsDirectory, hash[2..]);
    bool isBrandNewBlob = false;
    if (!System.IO.File.Exists(physicalPath))
    {
        using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        isBrandNewBlob = true;
        logger.LogInformation("[+] Saved new BLOB: {Hash}", hash);
    }

    try
    {
        var backupFile = new BackupFile
        {
            Hash = hash,
            RelativePath = relativePath,
            SessionId = sessionId,
            SizeBytes = file.Length
        };

        bool alreadyLinked = await db.Files.AnyAsync(f => f.SessionId == sessionId && f.Hash == hash);
        if (!alreadyLinked)
        {
            db.Files.Add(backupFile);
            await db.SaveChangesAsync();
        }


        logger.LogInformation("[+] File added to session {SessionId}: {RelativePath}", sessionId, relativePath);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "BD error during file save: {RelativePath}", relativePath);

        if (isBrandNewBlob && System.IO.File.Exists(physicalPath))
        {
            System.IO.File.Delete(physicalPath);
            logger.LogWarning("Transaction rollback: deleted orphaned blob {Hash}", hash);
        }

        return Results.StatusCode(500);
    }
}).DisableAntiforgery();

app.MapPost("/api/backup/finish", async ([FromQuery] int sessionId, BackupDbContext db, ILogger<Program> logger) =>
{
    var session = await db.Sessions.FindAsync(sessionId);
    if (session != null)
    {
        session.Status = BackupStatus.Completed;
        session.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Session {SessionId} completed successfully", sessionId);
    }

    return Results.Ok();
});

app.MapGet("/api/backup/sessions", async (string? clientName, BackupDbContext db) =>
{
    var query = db.Sessions.AsQueryable();

    if (!string.IsNullOrWhiteSpace(clientName))
    {
        query = query.Where(s => s.ClientName == clientName);
    }

    var sessions = await query
        .OrderByDescending(s => s.Id)
        .Take(20)
        .Select(s => new
        {
            Id = s.Id,
            ClientName = s.ClientName,
            Date = s.StartedAtUtc,
            Status = s.Status.ToString(),
            FilesCount = db.Files.Count(f => f.SessionId == s.Id)
        })
        .ToListAsync();

    return Results.Ok(sessions);
});

app.MapGet("/api/restore/{sessionId:int}/manifest", async (
    int sessionId,
    BackupDbContext db,
    ILogger<Program> logger) =>
{
    var sessionExists = await db.Sessions.AnyAsync(s => s.Id == sessionId);
    if (!sessionExists)
    {
        logger.LogWarning("Requested manifest for non-existent session: {SessionId}", sessionId);
        return Results.NotFound(new { Message = $"Session {sessionId} not found" });
    }


    var files = await db.Files
        .Where(f => f.SessionId == sessionId)
        .Select(f => new
        {
            Hash = f.Hash,
            RelativePath = f.RelativePath
        })
        .ToListAsync();

    logger.LogInformation("Providing manifest for session {SessionId} with {FileCount} files", sessionId, files.Count);
    return Results.Ok(files);
});
app.MapGet("/api/restore/blob/{hash}", (
    string hash,
    ServerConfig config,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(hash) || hash.Length < 3 || !hash.All(char.IsAsciiHexDigit))
    {
        return Results.BadRequest("Invalid hash format");
    }

    string shardFolder = hash[..2];
    string shardFileName = hash[2..];
    string physicalPath = Path.Combine(config.StoragePath, "blobs", shardFolder, shardFileName);

    if (!System.IO.File.Exists(physicalPath))
    {
        logger.LogError("CRITICAL ERROR: Blob {Hash} is listed in the database, but is missing from the disk!", hash);
        return Results.NotFound();
    }
    return Results.File(
        physicalPath,
        contentType: "application/octet-stream",
        enableRangeProcessing: true);
});
app.Run("http://localhost:5000");