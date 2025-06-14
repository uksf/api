using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerHelpers
{
    string GetGameServerExecutablePath(DomainGameServer gameServer);
    string GetGameServerSettingsPath();
    string GetGameServerMissionsPath();
    string GetGameServerConfigPath(DomainGameServer gameServer);
    string GetGameServerModsPaths(GameEnvironment environment);
    IEnumerable<string> GetGameServerExtraModsPaths();
    string FormatGameServerConfig(DomainGameServer gameServer, int playerCount, string missionSelection);
    string FormatGameServerLaunchArguments(DomainGameServer gameServer);
    string FormatHeadlessClientLaunchArguments(DomainGameServer gameServer, int index);
    string GetMaxPlayerCountFromConfig(DomainGameServer gameServer);
    int GetMaxCuratorCountFromSettings();
    TimeSpan StripMilliseconds(TimeSpan time);
    IEnumerable<Process> GetArmaProcesses();
    bool IsMainOpTime();
    string GetDlcModFoldersRegexString();
}

public class GameServerHelpers(IVariablesService variablesService, IUksfLogger logger) : IGameServerHelpers
{
    private static readonly string[] BaseConfig =
    [
        "hostname = \"{0}\";",
        "password = \"{1}\";",
        "passwordAdmin = \"{2}\";",
        "serverCommandPassword = \"brexit\";",
        "logFile = \"\";",
        "motd[] = {{\"\"}};",
        "motdInterval = 999999;",
        "maxPlayers = {3};",
        "kickDuplicate = 1;",
        "verifySignatures = 2;",
        "allowedFilePatching = 1;",
        "unsafeCVL = 1;",
        "disableVoN = 1;",
        "persistent = 1;",
        "timeStampFormat = \"short\";",
        "BattlEye = 0;",
        "disconnectTimeout = 30;",
        "onUserConnected = \"\";",
        "onUserDisconnected = \"\";",
        "doubleIdDetected = \"\";",
        "onUnsignedData = \"kick (_this select 0)\";",
        "onHackedData = \"\";",
        "onDifferentData = \"\";",
        "regularCheck = \"{{}}\";",
        "briefingTimeOut = -1;",
        "roleTimeOut = -1;",
        "votingTimeOut = -1;",
        "debriefingTimeOut = -1;",
        "lobbyIdleTimeout = -1;",
        "kickTimeout[] = {{{{0, 0}}, {{1, 0}}, {{2, 0}}, {{3, 0}}}};",
        "admins[] = {{\"76561198041153310\"}};",
        "headlessClients[] = {{\"127.0.0.1\"}};",
        "localClient[] = {{\"127.0.0.1\"}};",
        "forcedDifficulty = \"Custom\";",
        "class Missions {{",
        "    class Mission {{",
        "        template = \"{4}\";",
        "        difficulty = \"Custom\";",
        "    }};",
        "}};",
        "class AdvancedOptions {{",
        "    LogObjectNotFound = false;",
        "}};"
    ];

    public string GetGameServerExecutablePath(DomainGameServer gameServer)
    {
        var variableKey = gameServer.Environment switch
        {
            GameEnvironment.Release     => "SERVER_PATH_RELEASE",
            GameEnvironment.Rc          => "SERVER_PATH_RC",
            GameEnvironment.Development => "SERVER_PATH_DEV",
            _                           => throw new ArgumentException("Server environment is invalid")
        };
        return Path.Join(variablesService.GetVariable(variableKey).AsString(), "arma3server_x64.exe");
    }

    public string GetGameServerSettingsPath()
    {
        return Path.Join(variablesService.GetVariable("SERVER_PATH_RELEASE").AsString(), "userconfig", "cba_settings.sqf");
    }

    public string GetGameServerMissionsPath()
    {
        return variablesService.GetVariable("MISSIONS_PATH").AsString();
    }

    public string GetGameServerConfigPath(DomainGameServer gameServer)
    {
        return Path.Combine(variablesService.GetVariable("SERVER_PATH_CONFIGS").AsString(), $"{gameServer.ProfileName}.cfg");
    }

    public string GetGameServerModsPaths(GameEnvironment environment)
    {
        var variableKey = environment switch
        {
            GameEnvironment.Release     => "MODPACK_PATH_RELEASE",
            GameEnvironment.Rc          => "MODPACK_PATH_RC",
            GameEnvironment.Development => "MODPACK_PATH_DEV",
            _                           => throw new ArgumentException("Server environment is invalid")
        };
        return Path.Join(variablesService.GetVariable(variableKey).AsString(), "Repo");
    }

    public IEnumerable<string> GetGameServerExtraModsPaths()
    {
        return variablesService.GetVariable("SERVER_PATH_MODS").AsArray(x => x.RemoveQuotes());
    }

    public string FormatGameServerConfig(DomainGameServer gameServer, int playerCount, string missionSelection)
    {
        return string.Format(
            string.Join("\n", BaseConfig),
            gameServer.HostName,
            gameServer.Password,
            gameServer.AdminPassword,
            playerCount,
            missionSelection.Replace(".pbo", "")
        );
    }

    public string FormatGameServerLaunchArguments(DomainGameServer gameServer)
    {
        return $"-config={GetGameServerConfigPath(gameServer)}" +
               $" -profiles={GetGameServerProfilesPath(gameServer.Name)}" +
               $" -cfg={GetGameServerPerfConfigPath()}" +
               $" -name={gameServer.Name}" +
               $" -port={gameServer.Port}" +
               $" -apiport=\"{gameServer.ApiPort}\"" +
               $" {(string.IsNullOrEmpty(FormatGameServerServerMods(gameServer)) ? "" : $"\"-serverMod={FormatGameServerServerMods(gameServer)}\"")}" +
               $" {(string.IsNullOrEmpty(FormatGameServerMods(gameServer)) ? "" : $"\"-mod={FormatGameServerMods(gameServer)}\"")}" +
               " -bandwidthAlg=2 -hugepages -loadMissionToMemory -filePatching -limitFPS=200";
    }

    public string FormatHeadlessClientLaunchArguments(DomainGameServer gameServer, int index)
    {
        return $"-profiles={GetGameServerProfilesPath($"{gameServer.Name}{GetHeadlessClientName(index)}")}" +
               $" -name={GetHeadlessClientName(index)}" +
               $" -port={gameServer.Port}" +
               $" -apiport=\"{gameServer.ApiPort + index + 1}\"" +
               $" {(string.IsNullOrEmpty(FormatGameServerMods(gameServer)) ? "" : $"\"-mod={FormatGameServerMods(gameServer)}\"")}" +
               $" -password={gameServer.Password}" +
               " -localhost=127.0.0.1 -connect=localhost -client -hugepages -filePatching -limitFPS=200";
    }

    public string GetMaxPlayerCountFromConfig(DomainGameServer gameServer)
    {
        var maxPlayers = File.ReadAllLines(GetGameServerConfigPath(gameServer)).First(x => x.Contains("maxPlayers"));
        maxPlayers = maxPlayers.RemoveSpaces().Replace(";", "");
        return maxPlayers.Split("=")[1];
    }

    public int GetMaxCuratorCountFromSettings()
    {
        var lines = File.ReadAllLines(GetGameServerSettingsPath());
        var curatorsMaxString = lines.FirstOrDefault(x => x.Contains("uksf_curator_curatorsMax"));
        if (string.IsNullOrEmpty(curatorsMaxString))
        {
            logger.LogWarning("Could not find max curators in server settings file. Loading hardcoded deault '5'");
            return 5;
        }

        curatorsMaxString = curatorsMaxString.Split("=")[1].RemoveSpaces().Replace(";", "");
        return int.Parse(curatorsMaxString);
    }

    public TimeSpan StripMilliseconds(TimeSpan time)
    {
        return new TimeSpan(time.Hours, time.Minutes, time.Seconds);
    }

    public IEnumerable<Process> GetArmaProcesses()
    {
        return Process.GetProcesses().Where(x => x.ProcessName.StartsWith("arma3"));
    }

    public bool IsMainOpTime()
    {
        var now = DateTime.UtcNow;
        return now.DayOfWeek == DayOfWeek.Saturday && now.Hour >= 19 && now.Minute >= 30;
    }

    public string GetDlcModFoldersRegexString()
    {
        var dlcModFolders = variablesService.GetVariable("SERVER_DLC_MOD_FOLDERS").AsArray();
        return dlcModFolders.Select(x => $"(?<!.)({x})(?!.)").Aggregate((a, b) => $"{a}|{b}");
    }

    private static string FormatGameServerMods(DomainGameServer gameServer)
    {
        return gameServer.Mods.Count > 0 ? $"{string.Join(";", gameServer.Mods.Select(x => x.PathRelativeToServerExecutable ?? x.Path))};" : string.Empty;
    }

    private static string FormatGameServerServerMods(DomainGameServer gameServer)
    {
        return gameServer.ServerMods.Count > 0 ? $"{string.Join(";", gameServer.ServerMods.Select(x => x.Name))};" : string.Empty;
    }

    private string GetGameServerProfilesPath(string profile)
    {
        return Path.Combine(variablesService.GetVariable("SERVER_PATH_PROFILES").AsString(), profile);
    }

    private string GetGameServerPerfConfigPath()
    {
        return variablesService.GetVariable("SERVER_PATH_PERF").AsString();
    }

    private string GetHeadlessClientName(int index)
    {
        return variablesService.GetVariable("SERVER_HEADLESS_NAMES").AsArray()[index];
    }
}
