using System.IO;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class ConfigExportProcessLauncher(IProcessUtilities processUtilities, IVariablesService variablesService) : IConfigExportProcessLauncher
{
    private const string ProfileName = "ConfigExport";
    private const string ConfigFileName = "ConfigExport.cfg";
    private const string MissionName = "ConfigExport.VR";
    private const int GamePort = 3302;
    private const int ApiPort = 3303;

    public ConfigExportLaunchResult Launch(string modpackVersion)
    {
        var serverRoot = variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
        var configsRoot = variablesService.GetVariable("SERVER_PATH_CONFIGS").AsString();
        var profilesRoot = variablesService.GetVariable("SERVER_PATH_PROFILES").AsString();
        var modpackRoot = variablesService.GetVariable("MODPACK_PATH_RELEASE").AsString();
        var missionsRoot = variablesService.GetVariable("MISSIONS_PATH").AsString();
        var commandPassword = variablesService.GetVariable("SERVER_COMMAND_PASSWORD").AsString();

        var configPath = Path.Combine(configsRoot, ConfigFileName);
        var profilePath = Path.Combine(profilesRoot, ProfileName);
        var modPath = Path.Combine(modpackRoot, "Repo");
        var missionPath = Path.Combine(missionsRoot, MissionName);
        var executable = Path.Combine(serverRoot, "arma3server_x64.exe");

        Directory.CreateDirectory(profilePath);
        File.WriteAllText(configPath, BuildConfig(commandPassword));
        WriteMissionFiles(missionPath);

        var args = $"-server -autoInit" +
                   $" -config=\"{configPath}\"" +
                   $" -profiles=\"{profilePath}\"" +
                   $" {FormatModArg(modPath)}" +
                   $" -port={GamePort} -apiport={ApiPort}" +
                   $" -bandwidthAlg=2 -hugepages -filePatching -limitFPS=200";

        var pid = processUtilities.LaunchManagedProcess(executable, args);

        var expectedDir = Path.Combine(serverRoot, "uksf_config_export");
        var glob = $"config_*_uksf-{(modpackVersion ?? "").Replace('.', '-')}.cpp";

        return new ConfigExportLaunchResult(pid, expectedDir, glob);
    }

    // persistent = 1 in server.cfg + respawn = "INSTANT" in description.ext are both required
    // for -autoInit to actually fire (and INSTANT does not need a respawn marker — BASE would).
    private static string BuildConfig(string commandPassword) =>
        $$"""
          hostname = "Config Export";
          password = "";
          passwordAdmin = "";
          serverCommandPassword = "{{commandPassword}}";
          maxPlayers = 1;
          voteThreshold = 0.33;
          disableVoN = 1;
          persistent = 1;

          class Missions
          {
              class Mission
              {
                  template = "{{MissionName}}";
                  difficulty = "Custom";
              };
          };
          """;

    /// Builds a single -mod=... arg whose value is a semicolon-separated list of every
    /// @*-prefixed subdirectory directly under modPath. Arma's -mod= does NOT auto-discover
    /// @subdirs from a parent path, so each one has to be listed explicitly. Falls back to
    /// just the modPath if it contains no @subdirs (so the launcher works against a single
    /// mod folder layout too).
    private static string FormatModArg(string modPath)
    {
        if (!Directory.Exists(modPath)) return $"-mod=\"{modPath}\"";
        var modDirs = Directory.GetDirectories(modPath, "@*");
        if (modDirs.Length == 0) return $"-mod=\"{modPath}\"";
        return $"-mod=\"{string.Join(";", modDirs)}\"";
    }

    private static void WriteMissionFiles(string missionPath)
    {
        Directory.CreateDirectory(missionPath);
        var functionsDir = Path.Combine(missionPath, "functions");
        Directory.CreateDirectory(functionsDir);
        File.WriteAllText(Path.Combine(missionPath, "mission.sqm"), MissionSqm);
        File.WriteAllText(Path.Combine(missionPath, "description.ext"), DescriptionExt);
        File.WriteAllText(Path.Combine(functionsDir, "fn_runExport.sqf"), RunExportSqf);
    }

    // INSTANT respawn does not need a respawn_<side> marker (BASE does and falls back to
    // INSTANT silently if missing). A persistent mission requires either INSTANT or BASE
    // respawn AND server.cfg persistent = 1; for -autoInit to fire.
    //
    // CfgFunctions postInit = 1 fires the registered function in NON-SCHEDULED context after
    // mission init. initServer.sqf is scheduled (canSuspend=true per BI wiki "Available
    // Scripts" table) so a configFile traversal called from there gets throttled to ~3ms/frame
    // and takes 10x longer than the same work non-scheduled. postInit avoids that throttle.
    private const string DescriptionExt = """
                                          onLoadName = "Config Export";
                                          briefingName = "Config Export";
                                          respawn = "INSTANT";
                                          respawnDelay = 5;
                                          class Header
                                          {
                                              gameType = "Coop";
                                              minPlayers = 1;
                                              maxPlayers = 1;
                                          };
                                          class CfgFunctions
                                          {
                                              class UKSF
                                              {
                                                  class Export
                                                  {
                                                      file = "functions";
                                                      class runExport
                                                      {
                                                          postInit = 1;
                                                      };
                                                  };
                                              };
                                          };
                                          """;

    // Raw (unbinarized) mission.sqm needs binarizationWanted=0 + sourceName + explicit addons[]
    // + AddonsMetaData. Without these the engine auto-detects deps from entities, fails the DLC
    // validation path, and rejects the mission with "dependent on downloadable content that has
    // been deleted" — silent abort, autoInit no-ops. PBO-packed missions get this baked at
    // binarize time; raw missions must declare it.
    //
    // Playable B_Soldier_F (flags=7, isPlayable=1) gives -autoInit a slot to simulate "first
    // client" into; without it autoInit has nothing to initialise.
    private const string MissionSqm = """
                                      version=54;
                                      binarizationWanted=0;
                                      sourceName="ConfigExport";
                                      addons[]=
                                      {
                                          "A3_Characters_F"
                                      };
                                      class AddonsMetaData
                                      {
                                          class List
                                          {
                                              items=1;
                                              class Item0
                                              {
                                                  className="A3_Characters_F";
                                                  name="Arma 3 Alpha - Characters and Clothing";
                                                  author="Bohemia Interactive";
                                              };
                                          };
                                      };
                                      randomSeed=8237654;
                                      class ScenarioData
                                      {
                                          author="UKSF";
                                      };
                                      class Mission
                                      {
                                          class Intel
                                          {
                                          };
                                          class Entities
                                          {
                                              items=1;
                                              class Item0
                                              {
                                                  dataType="Group";
                                                  side="West";
                                                  class Entities
                                                  {
                                                      items=1;
                                                      class Item0
                                                      {
                                                          dataType="Object";
                                                          class PositionInfo
                                                          {
                                                              position[]={0,0,0};
                                                          };
                                                          side="West";
                                                          flags=7;
                                                          class Attributes
                                                          {
                                                              isPlayable=1;
                                                          };
                                                          id=0;
                                                          type="B_Soldier_F";
                                                      };
                                                  };
                                                  class Attributes
                                                  {
                                                  };
                                                  id=1;
                                              };
                                          };
                                      };
                                      class CustomAttributes
                                      {
                                      };
                                      """;

    // Registered as UKSF_Export_fnc_runExport via CfgFunctions postInit = 1 in description.ext.
    // Runs non-scheduled so the configFile traversal isn't throttled. We don't need inherited
    // properties — the consumer can walk the inheritance chain when querying. Skipping it
    // dramatically cuts both output size and SQF work (no per-class O(props × inheritance-depth)
    // dedup, no re-emission of parent props for every child class).
    private const string RunExportSqf = """
                                        private _result = [configFile, false] call uksf_common_fnc_configExport;

                                        if (_result isEqualTo "") then {
                                            diag_log text "ConfigExport.VR: uksf_common_fnc_configExport returned empty string — export failed";
                                        } else {
                                            diag_log text format ["ConfigExport.VR: config export completed successfully -> %1", _result];
                                        };

                                        "uksf" callExtension ["configExportFinish", []];
                                        """;
}
