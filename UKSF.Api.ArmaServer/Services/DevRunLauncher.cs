using System.IO;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class DevRunLauncher(ISyntheticServerLauncher syntheticLauncher, IVariablesService variablesService) : IDevRunLauncher
{
    private const int GamePort = 3304;
    private const int ApiPort = SyntheticApiPorts.DevRun;
    public const string MissionRunningLogLine = "uksf_dev: mission running, launching headless clients";

    public SyntheticLaunchResult Launch(string runId, string sqf, IReadOnlyList<string> mods, string worldName = null, int headlessClients = 0)
    {
        var serverRoot = variablesService.GetVariable("SERVER_PATH_DEV").AsString();
        var shortId = runId.Length >= 8 ? runId[..8] : runId;
        var profileName = $"DevRun_{shortId}";
        var effectiveWorld = string.IsNullOrWhiteSpace(worldName) ? "VR" : worldName;
        var missionName = $"{profileName}.{effectiveWorld}";

        var modpackPath = variablesService.GetVariable("MODPACK_REPO_PATH").AsString();
        var serverCbaSource = Path.Combine(modpackPath, "cba_settings.sqf");
        var userconfigDir = Path.Combine(serverRoot, "userconfig");
        if (File.Exists(serverCbaSource))
        {
            Directory.CreateDirectory(userconfigDir);
            File.Copy(serverCbaSource, Path.Combine(userconfigDir, "cba_settings.sqf"), overwrite: true);
        }

        var missionFiles = new Dictionary<string, string>();
        var missionCbaSource = Path.Combine(modpackPath, "UKSFTemplate.VR", "cba_settings.sqf");
        if (File.Exists(missionCbaSource))
        {
            missionFiles["cba_settings.sqf"] = File.ReadAllText(missionCbaSource);
        }

        var spec = new SyntheticLaunchSpec(
            ProfileName: profileName,
            ConfigFileName: $"{profileName}.cfg",
            MissionName: missionName,
            ServerExecutablePath: Path.Combine(serverRoot, "arma3server_x64.exe"),
            GamePort: GamePort,
            ApiPort: ApiPort,
            Mods: mods,
            ServerConfig: BuildConfig(profileName, missionName),
            MissionSqm: BuildMissionSqm(headlessClients),
            DescriptionExt: DescriptionExt,
            FunctionFiles: new Dictionary<string, string> { ["fn_runUserSqf.sqf"] = BuildWrapperSqf(runId, sqf, headlessClients) },
            MissionFiles: missionFiles,
            HeadlessClients: headlessClients
        );

        return syntheticLauncher.Launch(spec);
    }

    private static string BuildConfig(string profileName, string missionName) =>
        $$"""
          hostname = "{{profileName}}";
          password = "";
          passwordAdmin = "";
          maxPlayers = 4;
          voteThreshold = 0.33;
          disableVoN = 1;
          persistent = 1;
          headlessClients[] = {"127.0.0.1"};
          localClient[] = {"127.0.0.1"};

          class Missions
          {
              class Mission
              {
                  template = "{{missionName}}";
                  difficulty = "Custom";
              };
          };
          """;

    private static string BuildWrapperSqf(string runId, string userSqf, int headlessClients)
    {
        var escaped = userSqf.Replace("\"", "\"\"");
        return $$"""
                 uksf_dev_runId = "{{runId}}";
                 uksf_dev_resultPosted = false;
                 {{(headlessClients > 0 ? $"[\"{MissionRunningLogLine}\"] call uksf_dev_fnc_logLine;" : "")}}

                 private _userFn = compileFinal "{{escaped}}";
                 [] call _userFn;

                 if (!uksf_dev_resultPosted) then {
                     [nil] call uksf_dev_fnc_postResult;
                 };
                 """;
    }

    private const string DescriptionExt = """
                                          onLoadName = "DevRun";
                                          briefingName = "DevRun";
                                          respawn = "INSTANT";
                                          respawnDelay = 5;
                                          cba_settings_hasSettingsFile = 1;
                                          class Header
                                          {
                                              gameType = "Coop";
                                              minPlayers = 1;
                                              maxPlayers = 4;
                                          };
                                          class CfgFunctions
                                          {
                                              class UKSF
                                              {
                                                  class DevRun
                                                  {
                                                      file = "functions";
                                                      class runUserSqf
                                                      {
                                                          postInit = 1;
                                                      };
                                                  };
                                              };
                                          };
                                          """;

    private static string BuildMissionSqm(int headlessClients) =>
        MissionSqm.Replace("ENTITY_COUNT", (1 + headlessClients).ToString())
                  .Replace("HEADLESS_CLIENT_SLOTS", string.Concat(Enumerable.Range(1, headlessClients).Select(HeadlessClientSlot)));

    private static string HeadlessClientSlot(int index) =>
        $$"""
          class Item{{index}}
          {
              dataType="Logic";
              class PositionInfo
              {
                  position[]={{{index * 2}},0,0};
              };
              name="HC{{index}}";
              isPlayable=1;
              id={{100 + index}};
              type="HeadlessClient_F";
          };

          """;

    private const string MissionSqm = """
                                      version=54;
                                      binarizationWanted=0;
                                      sourceName="DevRun";
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
                                              items=ENTITY_COUNT;
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
                                      HEADLESS_CLIENT_SLOTS
                                          };
                                      };
                                      class CustomAttributes
                                      {
                                      };
                                      """;
}
