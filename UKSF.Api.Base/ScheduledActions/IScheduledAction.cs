namespace UKSF.Api.Base.ScheduledActions {
    public interface IScheduledAction {
        string Name { get; }
        void Run(params object[] parameters);
    }
}
