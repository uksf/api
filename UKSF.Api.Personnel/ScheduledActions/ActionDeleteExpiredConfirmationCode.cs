using System;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.ScheduledActions {
    public interface IActionDeleteExpiredConfirmationCode : IScheduledAction { }

    public class ActionDeleteExpiredConfirmationCode : IActionDeleteExpiredConfirmationCode {
        public const string ACTION_NAME = nameof(ActionDeleteExpiredConfirmationCode);

        private readonly IConfirmationCodeService confirmationCodeService;

        public ActionDeleteExpiredConfirmationCode(IConfirmationCodeService confirmationCodeService) => this.confirmationCodeService = confirmationCodeService;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            if (parameters.Length == 0) throw new ArgumentException("DeleteExpiredConfirmationCode action requires an id to be passed as a parameter, but no paramters were passed");
            string id = parameters[0].ToString();
            confirmationCodeService.Data.Delete(id);
        }
    }
}
