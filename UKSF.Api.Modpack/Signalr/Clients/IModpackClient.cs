using System.Threading.Tasks;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Signalr.Clients
{
    public interface IModpackClient
    {
        Task ReceiveReleaseCandidateBuild(ModpackBuild build);
        Task ReceiveBuild(ModpackBuild build);
        Task ReceiveBuildStep(ModpackBuildStep step);
    }
}
