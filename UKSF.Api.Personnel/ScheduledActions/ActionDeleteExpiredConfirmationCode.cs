using System;
using System.Threading.Tasks;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Personnel.Context;

namespace UKSF.Api.Personnel.ScheduledActions {
    public interface IActionDeleteExpiredConfirmationCode : IScheduledAction { }

    public class ActionDeleteExpiredConfirmationCode : IActionDeleteExpiredConfirmationCode {
        public const string ACTION_NAME = nameof(ActionDeleteExpiredConfirmationCode);

        private readonly IConfirmationCodeContext _confirmationCodeContext;

        public ActionDeleteExpiredConfirmationCode(IConfirmationCodeContext confirmationCodeContext) => _confirmationCodeContext = confirmationCodeContext;

        public string Name => ACTION_NAME;

        public Task Run(params object[] parameters) {
            if (parameters.Length == 0) {
                throw new ArgumentException("ActionDeleteExpiredConfirmationCode requires an id to be passed as a parameter, but no paramters were passed");
            }

            string id = parameters[0].ToString();
            _confirmationCodeContext.Delete(id);

            return Task.CompletedTask;
        }
    }
}
