using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Integrations.AppStart {
    public static class RegisterScheduledActions {
        public static void Register() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            IDeleteExpiredConfirmationCodeAction deleteExpiredConfirmationCodeAction = serviceProvider.GetService<IDeleteExpiredConfirmationCodeAction>();

            IScheduledActionService scheduledActionService = serviceProvider.GetService<IScheduledActionService>();
            scheduledActionService.RegisterScheduledActions(new HashSet<IScheduledAction> { deleteExpiredConfirmationCodeAction });
        }
    }
}
