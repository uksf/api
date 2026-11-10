using System.Collections.Generic;
using System.IO;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class GameDataExportProcessLauncher(ISyntheticServerLauncher syntheticLauncher, IVariablesService variablesService) : IGameDataExportProcessLauncher
{
    private const string ProfileName = "GameDataExport";
    private const string ConfigFileName = "GameDataExport.cfg";
    private const string MissionName = "GameDataExport.VR";
    private const int GamePort = 3302;
    private const int ApiPort = SyntheticApiPorts.GameDataExport;

    public GameDataExportLaunchResult Launch(string modpackVersion)
    {
        var serverRoot = variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
        var modpackRoot = variablesService.GetVariable("MODPACK_PATH_RELEASE").AsString();
        var commandPassword = variablesService.GetVariable("SERVER_COMMAND_PASSWORD").AsString();

        var spec = new SyntheticLaunchSpec(
            ProfileName: ProfileName,
            ConfigFileName: ConfigFileName,
            MissionName: MissionName,
            ServerExecutablePath: ArmaServerExecutable.PathIn(serverRoot),
            GamePort: GamePort,
            ApiPort: ApiPort,
            Mods: ResolveMods(Path.Combine(modpackRoot, "Repo")),
            ServerConfig: BuildConfig(commandPassword),
            MissionSqm: MissionSqm,
            DescriptionExt: DescriptionExt,
            FunctionFiles: new Dictionary<string, string> { ["fn_runExport.sqf"] = RunExportSqf }
        );

        var launch = syntheticLauncher.Launch(spec);

        var expectedDir = Path.Combine(serverRoot, "uksf_exports");
        var versionSegment = (modpackVersion ?? "").Replace('.', '-');
        return new GameDataExportLaunchResult(
            launch.ProcessId,
            expectedDir,
            ConfigGlob: $"config_*_uksf-{versionSegment}.cpp",
            CbaSettingsGlob: $"cba_settings_*_uksf-{versionSegment}.sqf",
            CbaSettingsReferenceGlob: $"cba_settings_reference_*_uksf-{versionSegment}.json"
        );
    }

    private IReadOnlyList<string> ResolveMods(string modPath)
    {
        var repoMods = ResolveRepoMods(modPath);
        var ocapPath = ResolveOcapPath();
        return ocapPath is null ? repoMods : [.. repoMods, ocapPath];
    }

    private static IReadOnlyList<string> ResolveRepoMods(string modPath)
    {
        if (!Directory.Exists(modPath)) return new[] { modPath };
        var modDirs = Directory.GetDirectories(modPath, "@*");
        return modDirs.Length == 0 ? new[] { modPath } : modDirs;
    }

    private string ResolveOcapPath()
    {
        var variable = variablesService.GetVariable("SERVER_PATH_MODS");
        if (variable?.Item is null) return null;

        var ocapPath = Path.Combine(variable.AsString(), "@ocap");
        return Directory.Exists(ocapPath) ? ocapPath : null;
    }

    private static string BuildConfig(string commandPassword) =>
        $$"""
          hostname = "Game Data Export";
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

    private const string DescriptionExt = """
                                          onLoadName = "Game Data Export";
                                          briefingName = "Game Data Export";
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

    private const string MissionSqm = """
                                      version=54;
                                      binarizationWanted=0;
                                      sourceName="GameDataExport";
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

    private const string RunExportSqf = """
                                        private _result = "";
                                        isNil { _result = [configFile, false] call uksf_common_fnc_gameDataExport; };

                                        if (_result isEqualTo "") then {
                                            diag_log text "GameDataExport.VR: uksf_common_fnc_gameDataExport returned empty string — export failed";
                                        } else {
                                            diag_log text format ["GameDataExport.VR: game data export completed successfully -> %1", _result];
                                        };

                                        "uksf" callExtension ["fileExportFinish", []];
                                        """;
}
