using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UKSF.Api.Models.Game;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.Services.Game {
    public static class GameServerHelpers {
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

        public static string GetGameServerExecutablePath() => VariablesWrapper.VariablesDataService().GetSingle("SERVER_EXECUTABLE").AsString();

        public static string GetGameServerSettingsPath() => VariablesWrapper.VariablesDataService().GetSingle("SERVER_SETTINGS").AsString();

        public static string GetGameServerMissionsPath() => VariablesWrapper.VariablesDataService().GetSingle("MISSIONS_PATH").AsString();

        public static string GetGameServerConfigPath(this GameServer gameServer) =>
            Path.Combine(VariablesWrapper.VariablesDataService().GetSingle("SERVERS_PATH").AsString(), "configs", $"{gameServer.profileName}.cfg");

        private static string GetGameServerProfilesPath() => VariablesWrapper.VariablesDataService().GetSingle("SERVER_PROFILES").AsString();

        private static string GetGameServerPerfConfigPath() => VariablesWrapper.VariablesDataService().GetSingle("SERVER_PERF_CONFIG").AsString();

        private static string GetHeadlessClientName(int index) => VariablesWrapper.VariablesDataService().GetSingle("HEADLESS_CLIENT_NAMES").AsArray()[index];

        private static string FormatGameServerMods(this GameServer gameServer) => gameServer.mods.Count > 0 ? $"{string.Join(";", gameServer.mods.Select(x => x.pathRelativeToServerExecutable ?? x.path))};" : string.Empty;

        public static IEnumerable<string> GetGameServerModsPaths() => VariablesWrapper.VariablesDataService().GetSingle("MODS_PATHS").AsArray(x => x.RemoveQuotes());

        public static string FormatGameServerConfig(this GameServer gameServer, int playerCount, string missionSelection) =>
            string.Format(string.Join("\n", BASE_CONFIG), gameServer.hostName, gameServer.password, gameServer.adminPassword, playerCount, missionSelection.Replace(".pbo", ""));

        public static string FormatGameServerLaunchArguments(this GameServer gameServer) =>
            $"-config={gameServer.GetGameServerConfigPath()}" +
            $" -profiles={GetGameServerProfilesPath()}" +
            $" -cfg={GetGameServerPerfConfigPath()}" +
            $" -name={gameServer.name}" +
            $" -port={gameServer.port}" +
            $" -apiport=\"{gameServer.apiPort}\"" +
            $" {(string.IsNullOrEmpty(gameServer.serverMods) ? "" : $"-serverMod={gameServer.serverMods}")}" +
            $" {(string.IsNullOrEmpty(gameServer.FormatGameServerMods()) ? "" : $"-mod={gameServer.FormatGameServerMods()}")}" +
            $" {(GetGameServerExecutablePath().Contains("server") ? "" : "-server")}" +
            " -bandwidthAlg=2 -hugepages -loadMissionToMemory -filePatching -limitFPS=200";

        public static string FormatHeadlessClientLaunchArguments(this GameServer gameServer, int index) =>
            $"-profiles={GetGameServerProfilesPath()}" +
            $" -name={GetHeadlessClientName(index)}" +
            $" -port={gameServer.port}" +
            $" -apiport=\"{gameServer.apiPort + index + 1}\"" +
            $" {(string.IsNullOrEmpty(gameServer.FormatGameServerMods()) ? "" : $"-mod={gameServer.FormatGameServerMods()}")}" +
            $" -password={gameServer.password}" +
            " -localhost=127.0.0.1 -connect=localhost -client -hugepages -filePatching -limitFPS=200";

        public static string GetMaxPlayerCountFromConfig(this GameServer gameServer) {
            string maxPlayers = File.ReadAllLines(gameServer.GetGameServerConfigPath()).First(x => x.Contains("maxPlayers"));
            maxPlayers = maxPlayers.RemoveSpaces().Replace(";", "");
            return maxPlayers.Split("=")[1];
        }

        public static int GetMaxCuratorCountFromSettings() {
            string[] lines = File.ReadAllLines(GetGameServerSettingsPath());
            string curatorsMaxString = lines.FirstOrDefault(x => x.Contains("uksf_curator_curatorsMax"));
            if (string.IsNullOrEmpty(curatorsMaxString)) {
                LogWrapper.Log("Could not find max curators in server settings file. Loading hardcoded deault '5'");
                return 5;
            }
            curatorsMaxString = curatorsMaxString.Split("=")[1].RemoveSpaces().Replace(";", "");
            return int.Parse(curatorsMaxString);
        }

        public static TimeSpan StripMilliseconds(this TimeSpan time) => new TimeSpan(time.Hours, time.Minutes, time.Seconds);

        public static IEnumerable<Process> GetArmaProcesses() => Process.GetProcesses().Where(x => x.ProcessName.StartsWith("arma3"));

        public static bool IsMainOpTime() {
            DateTime now = DateTime.UtcNow;
            return now.DayOfWeek == DayOfWeek.Saturday && now.Hour >= 19 && now.Minute >= 30;
        }
    }
}
