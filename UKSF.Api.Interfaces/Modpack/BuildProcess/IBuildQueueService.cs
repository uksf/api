using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildQueueService {
        void QueueBuild(ModpackBuild build);
        void Cancel(string id);
        void CancelAll();
    }
}
