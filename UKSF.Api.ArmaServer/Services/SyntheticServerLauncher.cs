using System.IO;
using Microsoft.Extensions.Configuration;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class SyntheticServerLauncher : ISyntheticServerLauncher
{
    private readonly IProcessUtilities _processUtilities;
    private readonly Func<string> _profilesRoot;
    private readonly Func<string> _configsRoot;
    private readonly Func<string> _missionsRoot;
    private readonly Func<string> _apiUrl;

    public SyntheticServerLauncher(IProcessUtilities processUtilities, IVariablesService variablesService, IConfiguration configuration) : this(
        processUtilities,
        () => variablesService.GetVariable("SERVER_PATH_PROFILES").AsString(),
        () => variablesService.GetVariable("SERVER_PATH_CONFIGS").AsString(),
        () => variablesService.GetVariable("MISSIONS_PATH").AsString(),
        () => configuration["Kestrel:Endpoints:Http:Url"]
    ) { }

    internal SyntheticServerLauncher(
        IProcessUtilities processUtilities,
        string profilesRoot,
        string configsRoot,
        string missionsRoot,
        string apiUrl = "http://localhost:5500"
    ) : this(processUtilities, () => profilesRoot, () => configsRoot, () => missionsRoot, () => apiUrl) { }

    private SyntheticServerLauncher(
        IProcessUtilities processUtilities,
        Func<string> profilesRoot,
        Func<string> configsRoot,
        Func<string> missionsRoot,
        Func<string> apiUrl
    )
    {
        _processUtilities = processUtilities;
        _profilesRoot = profilesRoot;
        _configsRoot = configsRoot;
        _missionsRoot = missionsRoot;
        _apiUrl = apiUrl;
    }

    public SyntheticLaunchResult Launch(SyntheticLaunchSpec spec)
    {
        var missing = spec.Mods.Where(p => !Directory.Exists(p)).ToList();
        if (missing.Count > 0) throw new InvalidModPathException(missing);

        var profilePath = Path.Combine(_profilesRoot(), spec.ProfileName);
        var configPath = Path.Combine(_configsRoot(), spec.ConfigFileName);
        var missionPath = Path.Combine(_missionsRoot(), spec.MissionName);
        var functionsDir = Path.Combine(missionPath, "functions");

        Directory.CreateDirectory(profilePath);
        Directory.CreateDirectory(missionPath);
        Directory.CreateDirectory(functionsDir);

        File.WriteAllText(configPath, spec.ServerConfig);
        File.WriteAllText(Path.Combine(missionPath, "mission.sqm"), spec.MissionSqm);
        File.WriteAllText(Path.Combine(missionPath, "description.ext"), spec.DescriptionExt);
        foreach (var (name, body) in spec.FunctionFiles)
        {
            File.WriteAllText(Path.Combine(functionsDir, name), body);
        }

        if (spec.MissionFiles is not null)
        {
            foreach (var (name, body) in spec.MissionFiles)
            {
                File.WriteAllText(Path.Combine(missionPath, name), body);
            }
        }

        var modList = string.Join(";", spec.Mods);
        var args = "-server -autoInit" +
                   $" -config=\"{configPath}\"" +
                   $" -profiles=\"{profilePath}\"" +
                   $" -mod=\"{modList}\"" +
                   $" -port={spec.GamePort} -apiport={spec.ApiPort}" +
                   $" -apiUrl=\"{_apiUrl()}\"" +
                   " -bandwidthAlg=2 -hugepages -filePatching -limitFPS=200";

        var pid = _processUtilities.LaunchManagedProcess(spec.ServerExecutablePath, args);
        return new SyntheticLaunchResult(pid, profilePath, missionPath);
    }
}
