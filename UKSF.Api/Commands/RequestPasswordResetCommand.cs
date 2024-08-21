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

public class RequestPasswordResetCommandArgs
{
    public RequestPasswordResetCommandArgs(string email)
    {
        Email = email;
    }

    public string Email { get; }
}

public class RequestPasswordResetCommand : IRequestPasswordResetCommand
{
    private readonly IAccountContext _accountContext;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IHostEnvironment _currentEnvironment;
    private readonly IUksfLogger _logger;
    private readonly ISendTemplatedEmailCommand _sendTemplatedEmailCommand;

    public RequestPasswordResetCommand(
        IAccountContext accountContext,
        IConfirmationCodeService confirmationCodeService,
        ISendTemplatedEmailCommand sendTemplatedEmailCommand,
        IUksfLogger logger,
        IHostEnvironment currentEnvironment
    )
    {
        _accountContext = accountContext;
        _confirmationCodeService = confirmationCodeService;
        _sendTemplatedEmailCommand = sendTemplatedEmailCommand;
        _logger = logger;
        _currentEnvironment = currentEnvironment;
    }

    public async Task ExecuteAsync(RequestPasswordResetCommandArgs args)
    {
        var account = _accountContext.GetSingle(x => string.Equals(x.Email, args.Email, StringComparison.InvariantCultureIgnoreCase));
        if (account == null)
        {
            return;
        }

        var code = await _confirmationCodeService.CreateConfirmationCode(account.Id);
        var url = BuildResetUrl(code);
        await _sendTemplatedEmailCommand.ExecuteAsync(
            new SendTemplatedEmailCommandArgs(
                account.Email,
                "UKSF Password Reset",
                TemplatedEmailNames.ResetPasswordTemplate,
                new Dictionary<string, string> { { "reset", url } }
            )
        );

        _logger.LogAudit($"Password reset request made for {account.Id}", account.Id);
    }

    private string BuildResetUrl(string code)
    {
        return _currentEnvironment.IsDevelopment() ? $"http://localhost:4200/login?reset={code}" : $"https://uk-sf.co.uk/login?reset={code}";
    }
}
