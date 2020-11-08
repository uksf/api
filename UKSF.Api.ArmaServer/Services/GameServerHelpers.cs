using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;

namespace UKSF.Api.ArmaServer.Services {
    public interface IGameServerHelpers {
        string GetGameServerExecutablePath(GameServer gameServer);
        string GetGameServerSettingsPath();
        string GetGameServerMissionsPath();
        string GetGameServerConfigPath(GameServer gameServer);
        string GetGameServerModsPaths(GameEnvironment environment);
        IEnumerable<string> GetGameServerExtraModsPaths();
        string FormatGameServerConfig(GameServer gameServer, int playerCount, string missionSelection);
        string FormatGameServerLaunchArguments(GameServer gameServer);
        string FormatHeadlessClientLaunchArguments(GameServer gameServer, int index);
        string GetMaxPlayerCountFromConfig(GameServer gameServer);
        int GetMaxCuratorCountFromSettings();
        TimeSpan StripMilliseconds(TimeSpan time);
        IEnumerable<Process> GetArmaProcesses();
        bool IsMainOpTime();
    }

    public class GameServerHelpers : IGameServerHelpers {
        private static readonly string[] BASE_CONFIG = {
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
            "onHackedData = \"kick (_this select 0)\";",
            "onDifferentData = \"kick (_this select 0)\";",
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
            "}};"
        };

        private readonly IVariablesService variablesService;
        private readonly ILogger logger;

        public GameServerHelpers(IVariablesService variablesService, ILogger logger) {
            this.variablesService = variablesService;
            this.logger = logger;
        }

        public string GetGameServerExecutablePath(GameServer gameServer) {
            string variableKey = gameServer.environment switch {
                GameEnvironment.RELEASE => "SERVER_PATH_RELEASE",
                GameEnvironment.RC      => "SERVER_PATH_RC",
                GameEnvironment.DEV     => "SERVER_PATH_DEV",
                _                       => throw new ArgumentException("Server environment is invalid")
            };
            return Path.Join(variablesService.GetVariable(variableKey).AsString(), "arma3server_x64.exe");
        }

        public string GetGameServerSettingsPath() => Path.Join(variablesService.GetVariable("SERVER_PATH_RELEASE").AsString(), "userconfig", "cba_settings.sqf");

        public string GetGameServerMissionsPath() => variablesService.GetVariable("MISSIONS_PATH").AsString();

        public string GetGameServerConfigPath(GameServer gameServer) => Path.Combine(variablesService.GetVariable("SERVER_PATH_CONFIGS").AsString(), $"{gameServer.profileName}.cfg");

        public string GetGameServerModsPaths(GameEnvironment environment) {
            string variableKey = environment switch {
                GameEnvironment.RELEASE => "MODPACK_PATH_RELEASE",
                GameEnvironment.RC      => "MODPACK_PATH_RC",
                GameEnvironment.DEV     => "MODPACK_PATH_DEV",
                _                       => throw new ArgumentException("Server environment is invalid")
            };
            return Path.Join(variablesService.GetVariable(variableKey).AsString(), "Repo");
        }

        public IEnumerable<string> GetGameServerExtraModsPaths() => variablesService.GetVariable("SERVER_PATH_MODS").AsArray(x => x.RemoveQuotes());

        public string FormatGameServerConfig(GameServer gameServer, int playerCount, string missionSelection) =>
            string.Format(string.Join("\n", BASE_CONFIG), gameServer.hostName, gameServer.password, gameServer.adminPassword, playerCount, missionSelection.Replace(".pbo", ""));

        public string FormatGameServerLaunchArguments(GameServer gameServer) =>
            $"-config={GetGameServerConfigPath(gameServer)}" +
            $" -profiles={GetGameServerProfilesPath()}" +
            $" -cfg={GetGameServerPerfConfigPath()}" +
            $" -name={gameServer.name}" +
            $" -port={gameServer.port}" +
            $" -apiport=\"{gameServer.apiPort}\"" +
            $" {(string.IsNullOrEmpty(FormatGameServerServerMods(gameServer)) ? "" : $"\"-serverMod={FormatGameServerServerMods(gameServer)}\"")}" +
            $" {(string.IsNullOrEmpty(FormatGameServerMods(gameServer)) ? "" : $"\"-mod={FormatGameServerMods(gameServer)}\"")}" +
            " -bandwidthAlg=2 -hugepages -loadMissionToMemory -filePatching -limitFPS=200";

        public string FormatHeadlessClientLaunchArguments(GameServer gameServer, int index) =>
            $"-profiles={GetGameServerProfilesPath()}" +
            $" -name={GetHeadlessClientName(index)}" +
            $" -port={gameServer.port}" +
            $" -apiport=\"{gameServer.apiPort + index + 1}\"" +
            $" {(string.IsNullOrEmpty(FormatGameServerMods(gameServer)) ? "" : $"\"-mod={FormatGameServerMods(gameServer)}\"")}" +
            $" -password={gameServer.password}" +
            " -localhost=127.0.0.1 -connect=localhost -client -hugepages -filePatching -limitFPS=200";

        public string GetMaxPlayerCountFromConfig(GameServer gameServer) {
            string maxPlayers = File.ReadAllLines(GetGameServerConfigPath(gameServer)).First(x => x.Contains("maxPlayers"));
            maxPlayers = maxPlayers.RemoveSpaces().Replace(";", "");
            return maxPlayers.Split("=")[1];
        }

        public int GetMaxCuratorCountFromSettings() {
            string[] lines = File.ReadAllLines(GetGameServerSettingsPath());
            string curatorsMaxString = lines.FirstOrDefault(x => x.Contains("uksf_curator_curatorsMax"));
            if (string.IsNullOrEmpty(curatorsMaxString)) {
                logger.LogWarning("Could not find max curators in server settings file. Loading hardcoded deault '5'");
                return 5;
            }

            curatorsMaxString = curatorsMaxString.Split("=")[1].RemoveSpaces().Replace(";", "");
            return int.Parse(curatorsMaxString);
        }

        public TimeSpan StripMilliseconds(TimeSpan time) => new TimeSpan(time.Hours, time.Minutes, time.Seconds);

        public IEnumerable<Process> GetArmaProcesses() => Process.GetProcesses().Where(x => x.ProcessName.StartsWith("arma3"));

        public bool IsMainOpTime() {
            DateTime now = DateTime.UtcNow;
            return now.DayOfWeek == DayOfWeek.Saturday && now.Hour >= 19 && now.Minute >= 30;
        }

        private string FormatGameServerMods(GameServer gameServer) =>
            gameServer.mods.Count > 0 ? $"{string.Join(";", gameServer.mods.Select(x => x.pathRelativeToServerExecutable ?? x.path))};" : string.Empty;

        private string FormatGameServerServerMods(GameServer gameServer) => gameServer.serverMods.Count > 0 ? $"{string.Join(";", gameServer.serverMods.Select(x => x.name))};" : string.Empty;

        private string GetGameServerProfilesPath() => variablesService.GetVariable("SERVER_PATH_PROFILES").AsString();

        private string GetGameServerPerfConfigPath() => variablesService.GetVariable("SERVER_PATH_PERF").AsString();

        private string GetHeadlessClientName(int index) => variablesService.GetVariable("SERVER_HEADLESS_NAMES").AsArray()[index];
    }
}
