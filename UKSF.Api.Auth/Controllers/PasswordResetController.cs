using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Auth.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Auth.Controllers
{
    [Route("[controller]")]
    public class PasswordResetController : ConfirmationCodeReceiver
    {
        private readonly IEmailService _emailService;
        private readonly ILogger _logger;

        public PasswordResetController(IConfirmationCodeService confirmationCodeService, ILoginService loginService, IEmailService emailService, IAccountContext accountContext, ILogger logger) : base(
            confirmationCodeService,
            loginService,
            accountContext
        )
        {
            _emailService = emailService;
            _logger = logger;
        }

        protected override async Task<IActionResult> ApplyValidatedPayload(string codePayload, Account account)
        {
            await AccountContext.Update(account.Id, x => x.Password, BCrypt.Net.BCrypt.HashPassword(codePayload));
            _logger.LogAudit($"Password changed for {account.Id}", account.Id);
            return Ok(LoginService.RegenerateBearerToken(account.Id));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JObject loginForm)
        {
            return await AttemptLoginValidatedAction(loginForm, "passwordreset");
        }

        [HttpPut]
        public async Task<IActionResult> ResetPassword([FromBody] JObject body)
        {
            Account account = AccountContext.GetSingle(x => string.Equals(x.Email, body["email"]?.ToString(), StringComparison.InvariantCultureIgnoreCase));
            if (account == null)
            {
                return BadRequest();
            }

            string code = await ConfirmationCodeService.CreateConfirmationCode(account.Id);
            string url = $"https://uk-sf.co.uk/login?validatecode={code}&validatetype={WebUtility.UrlEncode("password reset")}&validateurl={WebUtility.UrlEncode("passwordreset")}";
            string html = $"<h1>UKSF Password Reset</h1><br/>Please reset your password by clicking <strong><a href='{url}'>here</a></strong>." +
                          "<br/><br/><p>If this request was not made by you seek assistance from UKSF staff.</p>";
            _emailService.SendEmail(account.Email, "UKSF Password Reset", html);
            _logger.LogAudit($"Password reset request made for {account.Id}", account.Id);
            return Ok(LoginToken);
        }
    }
}
