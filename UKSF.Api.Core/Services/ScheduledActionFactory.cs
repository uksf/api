using UKSF.Api.Core.ScheduledActions;

namespace UKSF.Api.Core.Services;

public interface IScheduledActionFactory
{
    void RegisterScheduledActions(IEnumerable<IScheduledAction> newScheduledActions);
    IScheduledAction GetScheduledAction(string actionName);
}

public class ScheduledActionFactory : IScheduledActionFactory
{
    private readonly Dictionary<string, IScheduledAction> _scheduledActions = new();

    public void RegisterScheduledActions(IEnumerable<IScheduledAction> newScheduledActions)
    {
        foreach (var scheduledAction in newScheduledActions)
        {
            _scheduledActions[scheduledAction.Name] = scheduledAction;
        }
    }

    public IScheduledAction GetScheduledAction(string actionName)
    {
        if (_scheduledActions.TryGetValue(actionName, out var action))
        {
            return action;
        }

        throw new ArgumentException($"Failed to find action '{actionName}'");
    }
}
