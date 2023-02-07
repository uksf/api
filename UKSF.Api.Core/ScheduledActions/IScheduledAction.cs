namespace UKSF.Api.Core.ScheduledActions;

public interface IScheduledAction
{
    string Name { get; }
    Task Run(params object[] parameters);
}
