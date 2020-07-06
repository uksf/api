using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildQueueService {
        void QueueBuild(string version, ModpackBuild build);
        void Cancel();
    }
}
