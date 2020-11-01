using System;
using System.Collections.Generic;
using UKSF.Api.Utility.ScheduledActions;

namespace UKSF.Api.Utility.Services {
    public interface IScheduledActionService {
        void RegisterScheduledActions(IEnumerable<IScheduledAction> newScheduledActions);
        IScheduledAction GetScheduledAction(string actionName);
    }

    public class ScheduledActionService : IScheduledActionService {
        private readonly Dictionary<string, IScheduledAction> scheduledActions = new Dictionary<string, IScheduledAction>();

        public void RegisterScheduledActions(IEnumerable<IScheduledAction> newScheduledActions) {
            foreach (IScheduledAction scheduledAction in newScheduledActions) {
                scheduledActions[scheduledAction.Name] = scheduledAction;
            }
        }

        public IScheduledAction GetScheduledAction(string actionName) {
            if (scheduledActions.TryGetValue(actionName, out IScheduledAction action)) {
                return action;
            }

            throw new ArgumentException($"Failed to find action '{actionName}'");
        }
    }
}
