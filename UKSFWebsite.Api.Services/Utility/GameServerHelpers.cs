using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Data;

namespace UKSFWebsite.Api.Services.Utility {
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

        public static string GetGameServerExecutablePath() => VariablesWrapper.VariablesService().GetSingle("SERVER_EXECUTABLE").AsString();

        public static string GetGameServerMissionsPath() => VariablesWrapper.VariablesService().GetSingle("MISSIONS_PATH").AsString();

        public static string GetGameServerConfigPath(this GameServer gameServer) => Path.Combine(VariablesWrapper.VariablesService().GetSingle("SERVERS_PATH").AsString(), "configs", $"{gameServer.profileName}.cfg");

        public static string GetGameServerProfilesPath() => VariablesWrapper.VariablesService().GetSingle("SERVER_PROFILES").AsString();

        public static string GetGameServerPerfConfigPath() => VariablesWrapper.VariablesService().GetSingle("SERVER_PERF_CONFIG").AsString();

        public static string GetHeadlessClientName(int index) => VariablesWrapper.VariablesService().GetSingle("HEADLESS_CLIENT_NAMES").AsArray()[index];

        public static string FormatGameServerMods(this GameServer gameServer) => $"{string.Join(";", gameServer.mods.Select(x => x.pathRelativeToServerExecutable ?? x.path))};";

        public static IEnumerable<string> GetGameServerModsPaths() => VariablesWrapper.VariablesService().GetSingle("MODS_PATHS").AsArray(x => x.RemoveQuotes());

        public static string FormatGameServerConfig(this GameServer gameServer, int playerCount, string missionSelection) => string.Format(string.Join("\n", BASE_CONFIG), gameServer.hostName, gameServer.password, gameServer.adminPassword, playerCount, missionSelection.Replace(".pbo", ""));

        public static string FormatGameServerLaunchArguments(this GameServer gameServer) =>
            $"-config={gameServer.GetGameServerConfigPath()} -profiles={GetGameServerProfilesPath()} -cfg={GetGameServerPerfConfigPath()} -name={gameServer.name} -port={gameServer.port} -apiport=\"{gameServer.apiPort}\" {(string.IsNullOrEmpty(gameServer.serverMods) ? "" : $"-serverMod={gameServer.serverMods}")} -mod={gameServer.FormatGameServerMods()}{(!GetGameServerExecutablePath().Contains("server") ? " -server" : "")} -enableHT -high -bandwidthAlg=2 -hugepages -noSounds -loadMissionToMemory -filePatching";

        public static string FormatHeadlessClientLaunchArguments(this GameServer gameServer, int index) =>
            $"-profiles={GetGameServerProfilesPath()} -name={GetHeadlessClientName(index)} -port={gameServer.port} -apiport=\"{gameServer.apiPort + index + 1}\" -mod={gameServer.FormatGameServerMods()} -localhost=127.0.0.1 -connect=localhost -password={gameServer.password} -client -nosound -enableHT -high -hugepages -filePatching";

        public static string GetMaxPlayerCountFromConfig(this GameServer gameServer) {
            string maxPlayers = File.ReadAllLines(gameServer.GetGameServerConfigPath()).First(x => x.Contains("maxPlayers"));
            maxPlayers = maxPlayers.RemoveSpaces().Replace(";", "");
            return maxPlayers.Split("=")[1];
        }

        public static TimeSpan StripMilliseconds(this TimeSpan time) => new TimeSpan(time.Hours, time.Minutes, time.Seconds);

        public static IEnumerable<Process> GetArmaProcesses() => Process.GetProcesses().Where(x => x.ProcessName.StartsWith("arma3"));

        public static bool IsMainOpTime() {
            DateTime now = DateTime.UtcNow;
            return now.DayOfWeek == DayOfWeek.Saturday && now.Hour >= 19 && now.Minute >= 30;
        }
    }
}
