using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Game;
using UKSFWebsite.Api.Models.Mission;

namespace UKSFWebsite.Api.Interfaces.Game {
    public interface IGameServersService : IDataBackedService<IGameServersDataService> {
        int GetGameInstanceCount();
        Task UploadMissionFile(IFormFile file);
        List<MissionFile> GetMissionFiles();
        Task GetGameServerStatus(GameServer gameServer);
        Task<MissionPatchingResult> PatchMissionFile(string missionName);
        void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection);
        Task LaunchGameServer(GameServer gameServer);
        Task StopGameServer(GameServer gameServer);
        void KillGameServer(GameServer gameServer);
        int KillAllArmaProcesses();
        List<GameServerMod> GetAvailableMods();
    }
}
