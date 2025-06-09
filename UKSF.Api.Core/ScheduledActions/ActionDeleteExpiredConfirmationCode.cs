using UKSF.Api.Core.Context;

namespace UKSF.Api.Core.ScheduledActions;

public interface IActionDeleteExpiredConfirmationCode : IScheduledAction;

public class ActionDeleteExpiredConfirmationCode : IActionDeleteExpiredConfirmationCode
{
    public const string ActionName = nameof(ActionDeleteExpiredConfirmationCode);

    private readonly IConfirmationCodeContext _confirmationCodeContext;

    public ActionDeleteExpiredConfirmationCode(IConfirmationCodeContext confirmationCodeContext)
    {
        _confirmationCodeContext = confirmationCodeContext;
    }

    public string Name => ActionName;

    public Task Run(params object[] parameters)
    {
        if (parameters.Length == 0)
        {
            throw new ArgumentException("ActionDeleteExpiredConfirmationCode requires an id to be passed as a parameter, but no paramters were passed");
        }

        var id = parameters[0].ToString();
        _confirmationCodeContext.Delete(id);

        return Task.CompletedTask;
    }
}
