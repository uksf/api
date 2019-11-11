using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Mission;

namespace UKSFWebsite.Api.Interfaces.Game {
    public interface IMissionPatchingService {
        Task<MissionPatchingResult> PatchMission(string path);
    }
}
