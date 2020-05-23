using System.Collections.Generic;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.Interfaces.Utility {
    public interface IScheduledActionService {
        void RegisterScheduledActions(HashSet<IScheduledAction> newScheduledActions);
        IScheduledAction GetScheduledAction(string actionName);
    }
}
