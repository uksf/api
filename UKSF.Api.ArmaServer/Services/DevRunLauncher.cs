using System.IO;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class DevRunLauncher(ISyntheticServerLauncher syntheticLauncher, IVariablesService variablesService) : IDevRunLauncher
{
    private const int GamePort = 3304;
    private const int ApiPort = 3305;

    public SyntheticLaunchResult Launch(string runId, string sqf, IReadOnlyList<string> mods)
    {
        var serverRoot = variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
        var shortId = runId.Length >= 8 ? runId[..8] : runId;
        var profileName = $"DevRun_{shortId}";
        var missionName = $"{profileName}.VR";

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
            MissionSqm: MissionSqm,
            DescriptionExt: DescriptionExt,
            FunctionFiles: new Dictionary<string, string> { ["fn_runUserSqf.sqf"] = BuildWrapperSqf(runId, sqf) },
            MissionFiles: missionFiles
        );

        return syntheticLauncher.Launch(spec);
    }

    private static string BuildConfig(string profileName, string missionName) =>
        $$"""
          hostname = "{{profileName}}";
          password = "";
          passwordAdmin = "";
          maxPlayers = 1;
          voteThreshold = 0.33;
          disableVoN = 1;
          persistent = 1;

          class Missions
          {
              class Mission
              {
                  template = "{{missionName}}";
                  difficulty = "Custom";
              };
          };
          """;

    private static string BuildWrapperSqf(string runId, string userSqf)
    {
        var escaped = userSqf.Replace("\"", "\"\"");
        return $$"""
                 uksf_dev_runId = "{{runId}}";
                 uksf_dev_resultPosted = false;

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
                                              maxPlayers = 1;
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
}
