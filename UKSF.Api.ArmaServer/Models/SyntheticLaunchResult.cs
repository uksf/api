namespace UKSF.Api.ArmaServer.Models;

public record ProcessLaunch(string Executable, string Arguments);

public record SyntheticLaunchResult(int ProcessId, string ProfilePath, string MissionPath, IReadOnlyList<ProcessLaunch> PendingHeadlessClients = null);
