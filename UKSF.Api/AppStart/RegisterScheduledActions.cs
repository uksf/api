using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.AppStart {
    public static class RegisterScheduledActions {
        public static void Register() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            IDeleteExpiredConfirmationCodeAction deleteExpiredConfirmationCodeAction = serviceProvider.GetService<IDeleteExpiredConfirmationCodeAction>();
            IPruneLogsAction pruneLogsAction = serviceProvider.GetService<IPruneLogsAction>();
            ITeamspeakSnapshotAction teamspeakSnapshotAction = serviceProvider.GetService<ITeamspeakSnapshotAction>();

            IScheduledActionService scheduledActionService = serviceProvider.GetService<IScheduledActionService>();
            scheduledActionService.RegisterScheduledActions(new HashSet<IScheduledAction> { deleteExpiredConfirmationCodeAction, pruneLogsAction, teamspeakSnapshotAction });
        }
    }
}
