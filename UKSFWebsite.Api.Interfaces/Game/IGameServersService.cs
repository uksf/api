using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Game;
using UKSFWebsite.Api.Models.Mission;

namespace UKSFWebsite.Api.Interfaces.Game {
    public interface IGameServersService : IDataBackedService<IGameServersDataService> {
        int GetGameInstanceCount();
        Task<bool> UploadMissionFile(IFormFile file);
        List<MissionFile> GetMissionFiles();
        Task<MissionPatchingResult> PatchMissionFile(string missionName);
        void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection);
        Task LaunchGameServer(GameServer gameServer);
        Task StopGameServer(GameServer gameServer);
        Task KillGameServer(GameServer gameServer);
        Task<int> KillAllArmaProcesses();
        List<GameServerMod> GetAvailableMods();
        Task UpdateGameServerStatus(GameServerStatus gameServerStatus);
        GameServerStatus GetStatus(string key);
        List<GameServerStatus> GetAllStatuses();
        bool IsServerRunning(GameServer gameServer);
        bool IsServerRunning(Func<KeyValuePair<string, GameServerStatus>, bool> predicate);
        Task RemoveGameServerStatus(string key);
    }
}
