using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Controllers.Accounts {
    public abstract class ConfirmationCodeReceiver : Controller {
        protected readonly IConfirmationCodeService ConfirmationCodeService;
        internal readonly ILoginService LoginService;
        protected readonly IAccountService AccountService;
        protected string Logintoken;

        protected ConfirmationCodeReceiver(IConfirmationCodeService confirmationCodeService, ILoginService loginService, IAccountService accountService) {
            LoginService = loginService;
            ConfirmationCodeService = confirmationCodeService;
            AccountService = accountService;
        }

        protected abstract Task<IActionResult> ApplyValidatedPayload(string codePayload, Account account1);

        protected async Task<IActionResult> AttemptLoginValidatedAction(JObject loginForm, string codeType) {
            try {
                string validateCode = loginForm["code"].ToString();
                if (codeType == "passwordreset") {
                    Logintoken = LoginService.LoginWithoutPassword(loginForm["email"].ToString());
                    Account account = AccountService.GetSingle(x => string.Equals(x.email, loginForm["email"].ToString(), StringComparison.InvariantCultureIgnoreCase));
                    if (await ConfirmationCodeService.GetConfirmationCode(validateCode) == account.id && Logintoken != null) {
                        return await ApplyValidatedPayload(loginForm["password"].ToString(), account);
                    }
                } else {
                    Logintoken = LoginService.Login(loginForm["email"].ToString(), loginForm["password"].ToString());
                    Account account = AccountService.GetSingle(x => string.Equals(x.email, loginForm["email"].ToString(), StringComparison.InvariantCultureIgnoreCase));
                    string codeValue = await ConfirmationCodeService.GetConfirmationCode(validateCode);
                    if (!string.IsNullOrWhiteSpace(codeValue)) {
                        return await ApplyValidatedPayload(codeValue, account);
                    }
                }

                return BadRequest(new {message = "Code may have timed out or bad login"});
            } catch (LoginFailedException e) {
                return BadRequest(new {message = e.Message});
            }
        }
    }
}
