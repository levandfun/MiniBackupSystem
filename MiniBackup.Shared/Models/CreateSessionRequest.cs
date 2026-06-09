using System;
using System.Collections.Generic;
using System.Text;

namespace MiniBackup.Shared.Models;

public class CreateSessionRequest
{
    public string ClientName { get; set; } = string.Empty;
}
