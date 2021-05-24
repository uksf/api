using System;
using System.Threading.Tasks;
using UKSF.Api.Auth.Exceptions;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Auth.Commands
{
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
        private readonly ILogger _logger;

        public ResetPasswordCommand(IAccountContext accountContext, IConfirmationCodeService confirmationCodeService, ILogger logger)
        {
            _accountContext = accountContext;
            _confirmationCodeService = confirmationCodeService;
            _logger = logger;
        }

        public async Task ExecuteAsync(ResetPasswordCommandArgs args)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(x => string.Equals(x.Email, args.Email, StringComparison.InvariantCultureIgnoreCase));
            if (domainAccount == null)
            {
                throw new LoginFailedException("Password reset failed. No user found with that email");
            }

            string codeValue = await _confirmationCodeService.GetConfirmationCodeValue(args.Code);
            if (codeValue != domainAccount.Id)
            {
                throw new LoginFailedException("Password reset failed. Invalid code");
            }

            await _accountContext.Update(domainAccount.Id, x => x.Password, BCrypt.Net.BCrypt.HashPassword(args.Password));

            _logger.LogAudit($"Password changed for {domainAccount.Id}", domainAccount.Id);
        }
    }
}
