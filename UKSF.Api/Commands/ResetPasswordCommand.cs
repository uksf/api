﻿using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Commands;

public interface IResetPasswordCommand
{
    Task ExecuteAsync(ResetPasswordCommandArgs args);
}

public class ResetPasswordCommandArgs
{
    public ResetPasswordCommandArgs(string email, string password, string code)
    {
        Email = email;
        Password = password;
        Code = code;
    }

    public string Email { get; }
    public string Password { get; }
    public string Code { get; }
}

public class ResetPasswordCommand : IResetPasswordCommand
{
    private readonly IAccountContext _accountContext;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IUksfLogger _logger;

    public ResetPasswordCommand(IAccountContext accountContext, IConfirmationCodeService confirmationCodeService, IUksfLogger logger)
    {
        _accountContext = accountContext;
        _confirmationCodeService = confirmationCodeService;
        _logger = logger;
    }

    public async Task ExecuteAsync(ResetPasswordCommandArgs args)
    {
        var account = _accountContext.GetSingle(x => string.Equals(x.Email, args.Email, StringComparison.InvariantCultureIgnoreCase));
        if (account == null)
        {
            throw new BadRequestException("No user found with that email");
        }

        var codeValue = await _confirmationCodeService.GetConfirmationCodeValue(args.Code);
        if (codeValue != account.Id)
        {
            throw new BadRequestException("Password reset failed (Invalid code)");
        }

        await _accountContext.Update(account.Id, x => x.Password, BCrypt.Net.BCrypt.HashPassword(args.Password));

        _logger.LogAudit($"Password changed for {account.Id}", account.Id);
    }
}
