using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Mission;

namespace UKSF.Api.Interfaces.Game {
    public interface IGameServersService : IDataBackedService<IGameServersDataService> {
        int GetGameInstanceCount();
        Task UploadMissionFile(IFormFile file);
        List<MissionFile> GetMissionFiles();
        Task GetGameServerStatus(GameServer gameServer);
        Task<List<GameServer>> GetAllGameServerStatuses();
        Task<MissionPatchingResult> PatchMissionFile(string missionName);
        void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection);
        Task LaunchGameServer(GameServer gameServer);
        Task StopGameServer(GameServer gameServer);
        void KillGameServer(GameServer gameServer);
        int KillAllArmaProcesses();
        List<GameServerMod> GetAvailableMods(string id);
        List<GameServerMod> GetEnvironmentMods(GameServerEnvironment environment);
    }
}
