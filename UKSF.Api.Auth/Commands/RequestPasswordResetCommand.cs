using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Queries;

namespace UKSF.Api.Auth.Commands
{
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
        private readonly ILogger _logger;
        private readonly ISendTemplatedEmailCommand _sendTemplatedEmailCommand;

        public RequestPasswordResetCommand(
            IAccountContext accountContext,
            IConfirmationCodeService confirmationCodeService,
            ISendTemplatedEmailCommand sendTemplatedEmailCommand,
            ILogger logger,
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
            var domainAccount = _accountContext.GetSingle(x => string.Equals(x.Email, args.Email, StringComparison.InvariantCultureIgnoreCase));
            if (domainAccount == null)
            {
                return;
            }

            var code = await _confirmationCodeService.CreateConfirmationCode(domainAccount.Id);
            var url = BuildResetUrl(code);
            await _sendTemplatedEmailCommand.ExecuteAsync(new(domainAccount.Email, "UKSF Password Reset", TemplatedEmailNames.ResetPasswordTemplate, new() { { "reset", url } }));

            _logger.LogAudit($"Password reset request made for {domainAccount.Id}", domainAccount.Id);
        }

        private string BuildResetUrl(string code)
        {
            return _currentEnvironment.IsDevelopment() ? $"http://localhost:4200/login?reset={code}" : $"https://uk-sf.co.uk/login?reset={code}";
        }
    }
}
