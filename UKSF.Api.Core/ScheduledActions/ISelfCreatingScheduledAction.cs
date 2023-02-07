namespace UKSF.Api.Core.ScheduledActions;

public interface ISelfCreatingScheduledAction : IScheduledAction
{
    Task CreateSelf();
    Task Reset();
}
