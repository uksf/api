using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Queries;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Commands;

public interface IRequestPasswordResetCommand
{
    Task ExecuteAsync(RequestPasswordResetCommandArgs args);
}

public class RequestPasswordResetCommandArgs(string email)
{
    public string Email { get; } = email;
}

public class RequestPasswordResetCommand(
    IAccountContext accountContext,
    IConfirmationCodeService confirmationCodeService,
    ISendTemplatedEmailCommand sendTemplatedEmailCommand,
    IUksfLogger logger,
    IHostEnvironment currentEnvironment
) : IRequestPasswordResetCommand
{
    public async Task ExecuteAsync(RequestPasswordResetCommandArgs args)
    {
        var account = accountContext.GetSingle(x => string.Equals(x.Email, args.Email, StringComparison.InvariantCultureIgnoreCase));
        if (account == null)
        {
            return;
        }

        var code = await confirmationCodeService.CreateConfirmationCode(account.Id);
        var url = BuildResetUrl(code);
        await sendTemplatedEmailCommand.ExecuteAsync(
            new SendTemplatedEmailCommandArgs(
                account.Email,
                "UKSF Password Reset",
                TemplatedEmailNames.ResetPasswordTemplate,
                new Dictionary<string, string> { { "reset", url } }
            )
        );

        logger.LogAudit($"Password reset request made for {account.Id}", account.Id);
    }

    private string BuildResetUrl(string code)
    {
        return currentEnvironment.IsDevelopment() ? $"http://localhost:4200/login?reset={code}" : $"https://uk-sf.co.uk/login?reset={code}";
    }
}
