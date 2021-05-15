using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

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
            "kickDuplicate = 0;",
            "verifySignatures = 0;",
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
            "}};",
            "class AdvancedOptions {{",
            "    LogObjectNotFound = false;",
            "}};"
        };

        private readonly ILogger _logger;
        private readonly IVariablesService _variablesService;

        public GameServerHelpers(IVariablesService variablesService, ILogger logger) {
            _variablesService = variablesService;
            _logger = logger;
        }

        public string GetGameServerExecutablePath(GameServer gameServer) {
            string variableKey = gameServer.Environment switch {
                GameEnvironment.RELEASE => "SERVER_PATH_RELEASE",
                GameEnvironment.RC      => "SERVER_PATH_RC",
                GameEnvironment.DEV     => "SERVER_PATH_DEV",
                _                       => throw new ArgumentException("Server environment is invalid")
            };
            return Path.Join(_variablesService.GetVariable(variableKey).AsString(), "arma3server_x64.exe");
        }

        public string GetGameServerSettingsPath() {
            return Path.Join(_variablesService.GetVariable("SERVER_PATH_RELEASE").AsString(), "userconfig", "cba_settings.sqf");
        }

        public string GetGameServerMissionsPath() {
            return _variablesService.GetVariable("MISSIONS_PATH").AsString();
        }

        public string GetGameServerConfigPath(GameServer gameServer) {
            return Path.Combine(_variablesService.GetVariable("SERVER_PATH_CONFIGS").AsString(), $"{gameServer.ProfileName}.cfg");
        }

        public string GetGameServerModsPaths(GameEnvironment environment) {
            string variableKey = environment switch {
                GameEnvironment.RELEASE => "MODPACK_PATH_RELEASE",
                GameEnvironment.RC      => "MODPACK_PATH_RC",
                GameEnvironment.DEV     => "MODPACK_PATH_DEV",
                _                       => throw new ArgumentException("Server environment is invalid")
            };
            return Path.Join(_variablesService.GetVariable(variableKey).AsString(), "Repo");
        }

        public IEnumerable<string> GetGameServerExtraModsPaths() {
            return _variablesService.GetVariable("SERVER_PATH_MODS").AsArray(x => x.RemoveQuotes());
        }

        public string FormatGameServerConfig(GameServer gameServer, int playerCount, string missionSelection) {
            return string.Format(string.Join("\n", BASE_CONFIG), gameServer.HostName, gameServer.Password, gameServer.AdminPassword, playerCount, missionSelection.Replace(".pbo", ""));
        }

        public string FormatGameServerLaunchArguments(GameServer gameServer) {
            return $"-config={GetGameServerConfigPath(gameServer)}" +
                   $" -profiles={GetGameServerProfilesPath()}" +
                   $" -cfg={GetGameServerPerfConfigPath()}" +
                   $" -name={gameServer.Name}" +
                   $" -port={gameServer.Port}" +
                   $" -apiport=\"{gameServer.ApiPort}\"" +
                   $" {(string.IsNullOrEmpty(FormatGameServerServerMods(gameServer)) ? "" : $"\"-serverMod={FormatGameServerServerMods(gameServer)}\"")}" +
                   $" {(string.IsNullOrEmpty(FormatGameServerMods(gameServer)) ? "" : $"\"-mod={FormatGameServerMods(gameServer)}\"")}" +
                   " -bandwidthAlg=2 -hugepages -loadMissionToMemory -filePatching -limitFPS=200";
        }

        public string FormatHeadlessClientLaunchArguments(GameServer gameServer, int index) {
            return $"-profiles={GetGameServerProfilesPath()}" +
                   $" -name={GetHeadlessClientName(index)}" +
                   $" -port={gameServer.Port}" +
                   $" -apiport=\"{gameServer.ApiPort + index + 1}\"" +
                   $" {(string.IsNullOrEmpty(FormatGameServerMods(gameServer)) ? "" : $"\"-mod={FormatGameServerMods(gameServer)}\"")}" +
                   $" -password={gameServer.Password}" +
                   " -localhost=127.0.0.1 -connect=localhost -client -hugepages -filePatching -limitFPS=200";
        }

        public string GetMaxPlayerCountFromConfig(GameServer gameServer) {
            string maxPlayers = File.ReadAllLines(GetGameServerConfigPath(gameServer)).First(x => x.Contains("maxPlayers"));
            maxPlayers = maxPlayers.RemoveSpaces().Replace(";", "");
            return maxPlayers.Split("=")[1];
        }

        public int GetMaxCuratorCountFromSettings() {
            string[] lines = File.ReadAllLines(GetGameServerSettingsPath());
            string curatorsMaxString = lines.FirstOrDefault(x => x.Contains("uksf_curator_curatorsMax"));
            if (string.IsNullOrEmpty(curatorsMaxString)) {
                _logger.LogWarning("Could not find max curators in server settings file. Loading hardcoded deault '5'");
                return 5;
            }

            curatorsMaxString = curatorsMaxString.Split("=")[1].RemoveSpaces().Replace(";", "");
            return int.Parse(curatorsMaxString);
        }

        public TimeSpan StripMilliseconds(TimeSpan time) {
            return new(time.Hours, time.Minutes, time.Seconds);
        }

        public IEnumerable<Process> GetArmaProcesses() {
            return Process.GetProcesses().Where(x => x.ProcessName.StartsWith("arma3"));
        }

        public bool IsMainOpTime() {
            DateTime now = DateTime.UtcNow;
            return now.DayOfWeek == DayOfWeek.Saturday && now.Hour >= 19 && now.Minute >= 30;
        }

        private string FormatGameServerMods(GameServer gameServer) {
            return gameServer.Mods.Count > 0 ? $"{string.Join(";", gameServer.Mods.Select(x => x.PathRelativeToServerExecutable ?? x.Path))};" : string.Empty;
        }

        private string FormatGameServerServerMods(GameServer gameServer) {
            return gameServer.ServerMods.Count > 0 ? $"{string.Join(";", gameServer.ServerMods.Select(x => x.Name))};" : string.Empty;
        }

        private string GetGameServerProfilesPath() {
            return _variablesService.GetVariable("SERVER_PATH_PROFILES").AsString();
        }

        private string GetGameServerPerfConfigPath() {
            return _variablesService.GetVariable("SERVER_PATH_PERF").AsString();
        }

        private string GetHeadlessClientName(int index) {
            return _variablesService.GetVariable("SERVER_HEADLESS_NAMES").AsArray()[index];
        }
    }
}
