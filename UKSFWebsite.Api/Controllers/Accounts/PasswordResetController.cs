using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers.Accounts {
    [Route("[controller]")]
    public class PasswordResetController : ConfirmationCodeReceiver {
        private readonly IEmailService emailService;

        public PasswordResetController(IConfirmationCodeService confirmationCodeService, ILoginService loginService, IEmailService emailService, IAccountService accountService) : base(confirmationCodeService, loginService, accountService) => this.emailService = emailService;

        protected override async Task<IActionResult> ApplyValidatedPayload(string codePayload, Account account) {
            await AccountService.Update(account.id, "password", BCrypt.Net.BCrypt.HashPassword(codePayload));
            LogWrapper.AuditLog(account.id, $"Password changed for {account.id}");
            return Ok(LoginService.RegenerateToken(account.id));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JObject loginForm) => await AttemptLoginValidatedAction(loginForm, "passwordreset");

        [HttpPut]
        public async Task<IActionResult> ResetPassword([FromBody] JObject body) {
            Account account = AccountService.GetSingle(x => string.Equals(x.email, body["email"].ToString(), StringComparison.InvariantCultureIgnoreCase));
            if (account == null) {
                return BadRequest();
            }

            string code = await ConfirmationCodeService.CreateConfirmationCode(account.id);
            string url = $"https://uk-sf.co.uk/login?validatecode={code}&validatetype={WebUtility.UrlEncode("password reset")}&validateurl={WebUtility.UrlEncode("passwordreset")}";
            string html = $"<h1>UKSF Password Reset</h1><br/>Please reset your password by clicking <strong><a href='{url}'>here</a></strong>." + "<br/><br/><p>If this request was not made by you seek assistance from UKSF staff.</p>";
            emailService.SendEmail(account.email, "UKSF Password Reset", html);
            LogWrapper.AuditLog(account.id, $"Password reset request made for {account.id}");
            return Ok(LoginToken);
        }
    }
}
