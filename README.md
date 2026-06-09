Markdown
# MiniBackup System 🛡️

A lightweight yet architecturally mature client-server backup system. Built with C# (.NET) with a strict focus on performance, memory efficiency, and clean code.

## 🔥 Core Features Under the Hood

* **CAS Deduplication (Content-Addressable Storage):** Files are identified by their SHA-256 hashes. If multiple clients have identical files, the server stores the physical blob only once.
* **Stream-Based Transfer:** Transfers files of any size without loading them into RAM. The Agent consumes minimal memory, even when downloading terabytes of data.
* **Self-Healing Server:** The server validates not just the database records, but the physical presence of blobs on the disk during every manifest payload.
* **Multi-Tenancy:** Full backup isolation via `ClientName`. You can back up multiple machines to a single server instance.
* **Smart CLI:** A Git/Docker-style command-line interface for seamless management and restoration.
* **Fault Tolerance:** Automatic retries on network failures (powered by `Polly`).

## 🛠 Tech Stack

* **Backend:** ASP.NET Core Minimal API, Entity Framework Core (SQLite).
* **Agent:** .NET Console App, `HttpClient`, `SemaphoreSlim` (for concurrency).
* **Patterns:** Dependency Injection, N-Tier, Chunking/Batching.

---

## 🚀 Quick Start

### 1. Run the Server
The server automatically creates the `minibackup.db` database and the `blobs/` directory on its first run.
```powershell
cd MiniBackup.Server
dotnet run
```
2. Configure the Agent
Create a backup_config.json file next to MiniBackup.Agent.exe:
```JSON
{
  "client_name": "My-Work-PC",
  "server_url": "http://localhost:5000",
  "batch_size": 1000,
  "source_directories": [
    "C:\\ImportantFiles"
  ]
}
```
💻 CLI Commands (Agent)
Create a backup (reads config by default):

```PowerShell
.\MiniBackup.Agent.exe
View backup history:
```
```PowerShell

.\MiniBackup.Agent.exe list
Restore the latest backup:
```
```PowerShell
.\MiniBackup.Agent.exe restore latest C:\RecoveredFiles
Restore a specific session (by ID):
```
```PowerShell
.\MiniBackup.Agent.exe restore 42 C:\RecoveredFiles
```
On-the-Fly Parameter Overrides
You can bypass backup_config.json on the fly using the --client and --server flags. Perfect for restoring files from another machine:

```PowerShell
# View backups of a broken laptop from a new PC:
.\MiniBackup.Agent.exe list --client "Old-Laptop"
```
# Download files from a different machine using a custom server:
```
.\MiniBackup.Agent.exe restore latest C:\Recover --client "Old-Laptop" --server "[http://192.168.1.100:5000](http://192.168.1.100:5000)"
```
