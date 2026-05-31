using System;
using System.Collections.Generic;
using System.Text;

namespace MiniBackup.Shared.Models;

public class BackupSessionDto
{
    public int Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FilesCount { get; set; }
}