using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class PasswordResetController : ConfirmationCodeReceiver {
        private readonly IEmailService emailService;
        private readonly ILogger logger;

        public PasswordResetController(IConfirmationCodeService confirmationCodeService, ILoginService loginService, IEmailService emailService, IAccountService accountService, ILogger logger) : base(
            confirmationCodeService,
            loginService,
            accountService
        ) {
            this.emailService = emailService;
            this.logger = logger;
        }

        protected override async Task<IActionResult> ApplyValidatedPayload(string codePayload, Account account) {
            await AccountService.Data.Update(account.id, "password", BCrypt.Net.BCrypt.HashPassword(codePayload));
            logger.LogAudit($"Password changed for {account.id}", account.id);
            return Ok(LoginService.RegenerateToken(account.id));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JObject loginForm) => await AttemptLoginValidatedAction(loginForm, "passwordreset");

        [HttpPut]
        public async Task<IActionResult> ResetPassword([FromBody] JObject body) {
            Account account = AccountService.Data.GetSingle(x => string.Equals(x.email, body["email"]?.ToString(), StringComparison.InvariantCultureIgnoreCase));
            if (account == null) {
                return BadRequest();
            }

            string code = await ConfirmationCodeService.CreateConfirmationCode(account.id);
            string url = $"https://uk-sf.co.uk/login?validatecode={code}&validatetype={WebUtility.UrlEncode("password reset")}&validateurl={WebUtility.UrlEncode("passwordreset")}";
            string html = $"<h1>UKSF Password Reset</h1><br/>Please reset your password by clicking <strong><a href='{url}'>here</a></strong>." +
                          "<br/><br/><p>If this request was not made by you seek assistance from UKSF staff.</p>";
            emailService.SendEmail(account.email, "UKSF Password Reset", html);
            logger.LogAudit($"Password reset request made for {account.id}", account.id);
            return Ok(LoginToken);
        }
    }
}
