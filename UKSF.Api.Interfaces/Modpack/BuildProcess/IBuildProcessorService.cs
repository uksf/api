using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildProcessorService {
        Task ProcessBuild(string id, ModpackBuild build, CancellationTokenSource cancellationTokenSource);
    }
}
