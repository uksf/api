using System.Threading.Tasks;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Hubs {
    public interface IModpackClient {
        Task ReceiveBuildRelease(ModpackBuildRelease buildRelease);
        Task ReceiveBuild(string version, ModpackBuild build);
        Task ReceiveBuildStep(ModpackBuildStep step);
    }
}
