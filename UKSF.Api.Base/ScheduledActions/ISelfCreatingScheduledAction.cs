using System.Threading.Tasks;

namespace UKSF.Api.Base.ScheduledActions {
    public interface ISelfCreatingScheduledAction : IScheduledAction {
        Task CreateSelf();
        Task Reset();
    }
}
