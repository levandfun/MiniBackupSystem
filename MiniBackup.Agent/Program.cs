using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniBackup.Agent.Caching;
using MiniBackup.Agent.Extensions;
using MiniBackup.Agent.Services;
using MiniBackup.Shared.Models;
using Serilog;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/agent_log_.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{

    Log.Information("Starting Agent Host...");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHttpClient();
            services.AddTransient<IBackupEngine, BackupEngine>();
            services.AddTransient<IHashCalculator, HashCalculator>();
            services.AddTransient<IFileScanner, FileScanner>();
            services.AddTransient<IConfigReader, JsonConfigReader>();
            services.AddSingleton<ILocalStateCache, LocalStateCache>();
            services.AddTransient<IRestoreEngine, RestoreEngine>();

            services.AddNetworkServices();
        })
        .Build();


    string configPath = "backup_config.json";
    string clientName = Environment.MachineName;
    string serverUrl = "http://localhost:5000";
    BackupJobConfig? backupConfig = null;

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].ToLower() == "--config" && i + 1 < args.Length)
            configPath = args[i + 1];
        if (args[i].ToLower() == "--client" && i + 1 < args.Length)
            clientName = args[i + 1];
        if (args[i].ToLower() == "--server" && i + 1 < args.Length)
            serverUrl = args[i + 1];
    }

    if (File.Exists(configPath))
    {
        string json = File.ReadAllText(configPath);
        backupConfig = JsonSerializer.Deserialize<BackupJobConfig>(json);

        if (backupConfig != null)
        {
            if (!string.IsNullOrWhiteSpace(backupConfig.ServerUrl))
                serverUrl = backupConfig.ServerUrl;
            if (!string.IsNullOrWhiteSpace(backupConfig.ClientName))
                clientName = backupConfig.ClientName;
        }
    }

    // CLI flags override config
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].ToLower() == "--client" && i + 1 < args.Length)
        {
            clientName = args[i + 1];
            if (backupConfig != null)  backupConfig.ClientName = clientName;
        }
        if (args[i].ToLower() == "--server" && i + 1 < args.Length)
        {
            serverUrl = args[i + 1];
            if (backupConfig != null)  backupConfig.ServerUrl = serverUrl;

        }
    }

    var cancellationTokenSource = new CancellationTokenSource();
    string command = args.Length > 0 && !args[0].StartsWith("--")
        ? args[0].ToLower()
        : string.Empty;

    if (command == "list")
    {
        var networkClient = host.Services.GetRequiredService<INetworkClient>();
        var sessions = await networkClient.GetSessionsAsync(serverUrl, clientName, cancellationTokenSource.Token);
        if (sessions == null || sessions.Count == 0)
        {
            Console.WriteLine("No sessions found.");
            return;
        }
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"ID",-5} | {"DATE (UTC)",-20} | {"FILES",-10} | STATUS");
        Console.WriteLine(new string('-', 60));
        foreach (var s in sessions)
            Console.WriteLine($"{s.Id,-5} | {s.Date:yyyy-MM-dd HH:mm:ss}  | {s.FilesCount,-10} | {s.Status}");
        Console.WriteLine(new string('-', 60));
        return;
    }

    if (command == "restore" && args.Length >= 3)
    {
        var networkClient = host.Services.GetRequiredService<INetworkClient>();
        string sessionArg = args[1].ToLower();
        string targetPath = args[2];
        int sessionId = -1;

        if (sessionArg == "latest")
        {
            var sessions = await networkClient.GetSessionsAsync(serverUrl, clientName, cancellationTokenSource.Token);
            var latest = sessions?.FirstOrDefault(s => s.Status == "Completed");
            if (latest == null)
            {
                Console.WriteLine("No completed backups found.");
                return;
            }
            sessionId = latest.Id;
            Console.WriteLine($"Found backup #{sessionId} from {latest.Date:dd.MM.yyyy HH:mm}");
        }
        else
        {
            int.TryParse(sessionArg, out sessionId);
        }

        if (sessionId <= 0)
        {
            Console.WriteLine("Error: invalid session ID.");
            return;
        }

        int threads = Math.Max(2, Environment.ProcessorCount);
        if (args.Length >= 4 && int.TryParse(args[3], out int parsedThreads))
            threads = parsedThreads;

        var restoreEngine = host.Services.GetRequiredService<IRestoreEngine>();
        await restoreEngine.RunRestoreAsync(serverUrl, sessionId, targetPath, cancellationTokenSource.Token, threads);
        return;
    }

    if (!string.IsNullOrEmpty(command))
    {
        Console.WriteLine($"Unknown command: {command}");
        return;
    }

    if (backupConfig == null)
    {
        Console.WriteLine($"Error: config file '{configPath}' not found.");
        return;
    }

    var engine = host.Services.GetRequiredService<IBackupEngine>();
    await engine.RunAsync(backupConfig, cancellationTokenSource.Token);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}