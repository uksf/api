namespace UKSF.Api.Shared.ScheduledActions;

public interface ISelfCreatingScheduledAction : IScheduledAction
{
    Task CreateSelf();
    Task Reset();
}
