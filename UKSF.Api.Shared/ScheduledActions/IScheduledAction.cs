namespace UKSF.Api.Shared.ScheduledActions;

public interface IScheduledAction
{
    string Name { get; }
    Task Run(params object[] parameters);
}
