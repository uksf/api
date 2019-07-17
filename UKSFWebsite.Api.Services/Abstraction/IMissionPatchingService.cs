using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Mission;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IMissionPatchingService {
        Task<MissionPatchingResult> PatchMission(string path);
    }
}
