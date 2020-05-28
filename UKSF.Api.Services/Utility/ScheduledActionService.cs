using System;
using System.Collections.Generic;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.Services.Utility {
    public class ScheduledActionService : IScheduledActionService {
        private readonly Dictionary<string, IScheduledAction> scheduledActions = new Dictionary<string, IScheduledAction>();

        public void RegisterScheduledActions(HashSet<IScheduledAction> newScheduledActions) {
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
