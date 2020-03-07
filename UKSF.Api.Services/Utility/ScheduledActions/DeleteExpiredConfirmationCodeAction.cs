using System;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.Services.Utility.ScheduledActions {
    public class DeleteExpiredConfirmationCodeAction : IDeleteExpiredConfirmationCodeAction {
        public const string ACTION_NAME = nameof(DeleteExpiredConfirmationCodeAction);

        private readonly IConfirmationCodeService confirmationCodeService;

        public DeleteExpiredConfirmationCodeAction(IConfirmationCodeService confirmationCodeService) => this.confirmationCodeService = confirmationCodeService;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            if (parameters.Length == 0) throw new ArgumentException("DeleteExpiredConfirmationCode action requires an id to be passed as a parameter");
            string id = parameters[0].ToString();
            confirmationCodeService.Data.Delete(id);
        }
    }
}
