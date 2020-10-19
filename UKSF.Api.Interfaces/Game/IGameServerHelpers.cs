using System;
using System.Collections.Generic;
using System.Diagnostics;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Interfaces.Game {
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
}
