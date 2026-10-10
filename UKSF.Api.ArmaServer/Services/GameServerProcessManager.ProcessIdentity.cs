using System.ComponentModel;
using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1);

    internal bool IsServerProcess(DomainGameServer server, Process process)
    {
        if (server.ProcessStartedAt is not { } recorded || StartTimeOf(process) is not { } actual)
        {
            return true;
        }

        return (actual - recorded).Duration() <= StartTimeTolerance;
    }

    internal DateTime? StartTimeOf(Process process)
    {
        try
        {
            return process?.StartTime.ToUniversalTime();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private DateTime? StartTimeOfProcessId(int? processId) => processId is { } id ? StartTimeOf(processUtilities.FindProcessById(id)) : null;
}
