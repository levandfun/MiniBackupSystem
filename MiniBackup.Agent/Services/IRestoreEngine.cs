using System;
using System.Collections.Generic;
using System.Text;

namespace MiniBackup.Agent.Services;

public interface IRestoreEngine
{
    Task RunRestoreAsync(string serverUrl, int sessionId, string targetDirectory, CancellationToken token, int maxConcurrency);
}


