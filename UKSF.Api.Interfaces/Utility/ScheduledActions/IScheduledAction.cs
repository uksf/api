namespace UKSF.Api.Interfaces.Utility.ScheduledActions {
    public interface IScheduledAction {
        string Name { get; }
        void Run(params object[] parameters);
    }
}
