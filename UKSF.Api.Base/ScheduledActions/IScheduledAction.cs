using System.Threading.Tasks;

namespace UKSF.Api.Base.ScheduledActions {
    public interface IScheduledAction {
        string Name { get; }
        Task Run(params object[] parameters);
    }
}
