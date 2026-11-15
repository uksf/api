using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IDevRunLauncher
{
    IReadOnlyList<string> CleanUpPreviousRuns();

    SyntheticLaunchResult Launch(string runId, string sqf, IReadOnlyList<string> mods, string worldName = null, int headlessClients = 0);
}
