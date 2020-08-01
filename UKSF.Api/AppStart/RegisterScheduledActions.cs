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
            IInstagramImagesAction instagramImagesAction = serviceProvider.GetService<IInstagramImagesAction>();
            IInstagramTokenAction instagramTokenAction = serviceProvider.GetService<IInstagramTokenAction>();
            IPruneDataAction pruneDataAction = serviceProvider.GetService<IPruneDataAction>();
            ITeamspeakSnapshotAction teamspeakSnapshotAction = serviceProvider.GetService<ITeamspeakSnapshotAction>();

            IScheduledActionService scheduledActionService = serviceProvider.GetService<IScheduledActionService>();
            scheduledActionService.RegisterScheduledActions(new HashSet<IScheduledAction> { deleteExpiredConfirmationCodeAction, instagramImagesAction, instagramTokenAction, pruneDataAction, teamspeakSnapshotAction });
        }
    }
}
