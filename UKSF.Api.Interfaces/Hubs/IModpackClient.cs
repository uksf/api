using System.Threading.Tasks;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Hubs {
    public interface IModpackClient {
        Task ReceiveReleaseCandidateBuild(ModpackBuild build);
        Task ReceiveBuild(ModpackBuild build);
        Task ReceiveBuildStep(ModpackBuildStep step);
        Task ReceiveBuildStepLog(ModpackBuildStepLogItemUpdate logUpdate);
        Task ReceiveLargeBuildStep(int index);
    }
}
