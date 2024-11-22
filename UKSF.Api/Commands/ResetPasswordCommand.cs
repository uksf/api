using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Commands;

public interface IResetPasswordCommand
{
    Task ExecuteAsync(ResetPasswordCommandArgs args);
}

public class ResetPasswordCommandArgs(string email, string password, string code)
{
    public string Email { get; } = email;
    public string Password { get; } = password;
    public string Code { get; } = code;
}

public class ResetPasswordCommand(IAccountContext accountContext, IConfirmationCodeService confirmationCodeService, IUksfLogger logger) : IResetPasswordCommand
{
    public async Task ExecuteAsync(ResetPasswordCommandArgs args)
    {
        var account = accountContext.GetSingle(x => string.Equals(x.Email, args.Email, StringComparison.InvariantCultureIgnoreCase));
        if (account == null)
        {
            throw new BadRequestException("No user found with that email");
        }

        var codeValue = await confirmationCodeService.GetConfirmationCodeValue(args.Code);
        if (codeValue != account.Id)
        {
            throw new BadRequestException("Password reset failed (Invalid code)");
        }

        await accountContext.Update(account.Id, x => x.Password, BCrypt.Net.BCrypt.HashPassword(args.Password));

        logger.LogAudit($"Password changed for {account.Id}", account.Id);
    }
}
