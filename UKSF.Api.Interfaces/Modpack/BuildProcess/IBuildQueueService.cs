using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IBuildQueueService {
        void QueueBuild(ModpackBuild build);
        bool CancelQueued(string id);
        void Cancel(string id);
        void CancelAll();
    }
}
