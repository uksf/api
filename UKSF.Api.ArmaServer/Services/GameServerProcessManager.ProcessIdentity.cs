using System.ComponentModel;
using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    internal static DateTime CanonicalStartTime(DateTime time)
    {
        var utc = time.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    internal DateTime? StartTimeOf(Process process)
    {
        try
        {
            return process is null ? null : CanonicalStartTime(process.StartTime);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    internal TrackedProcess Track(int processId) =>
        StartTimeOf(processUtilities.FindProcessById(processId)) is { } startedAt ? new TrackedProcess(processId, startedAt) : null;

    internal Process TryOpenOwnedProcess(TrackedProcess tracked)
    {
        if (tracked is null || !gameServerHelpers.IsOwnedProcessId(tracked.ProcessId))
        {
            return null;
        }

        var process = processUtilities.FindProcessById(tracked.ProcessId);
        try
        {
            return process is { HasExited: false } && StartTimeOf(process) == tracked.StartedAt ? process : null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private TrackedProcess ServerProcessOf(DomainGameServer server)
    {
        if (server.ServerProcess is not null || server.ProcessId is not { } processId)
        {
            return server.ServerProcess;
        }

        var listed = FindMatchingServerProcess(server, gameServerHelpers.GetGameServerArmaProcesses());
        if (listed?.ProcessId != processId || Track(processId) is not { } adopted)
        {
            return null;
        }

        server.ProcessStartedAt = adopted.StartedAt;
        return adopted;
    }

    private List<TrackedProcess> HeadlessClientsOf(DomainGameServer server, IReadOnlyList<ProcessCommandLineInfo> armaProcesses) =>
        server.HeadlessClients.Concat(
                  (armaProcesses ?? gameServerHelpers.GetGameServerArmaProcesses())
                  .Where(process => MatchesPort(process, server.Port) && process.CommandLine.Contains("-client"))
                  .Select(process => Track(process.ProcessId))
                  .Where(tracked => tracked is not null)
              )
              .Distinct()
              .ToList();

    private static bool PredatesProcess(DomainGameServer server, DateTime? enqueuedAt) =>
        enqueuedAt is { } sent && server.ProcessStartedAt is { } started && sent < started;
}
