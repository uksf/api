using System.Threading.Tasks;
using UKSF.Api.Models.Mission;

namespace UKSF.Api.Interfaces.Game {
    public interface IMissionPatchingService {
        Task<MissionPatchingResult> PatchMission(string path);
    }
}
