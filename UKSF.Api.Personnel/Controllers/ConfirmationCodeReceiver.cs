﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    public abstract class ConfirmationCodeReceiver : Controller {
        protected readonly IAccountService AccountService;
        protected readonly IConfirmationCodeService ConfirmationCodeService;
        internal readonly ILoginService LoginService;
        protected string LoginToken;

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
                    LoginToken = LoginService.LoginWithoutPassword(loginForm["email"].ToString());
                    Account account = AccountService.Data.GetSingle(x => string.Equals(x.email, loginForm["email"].ToString(), StringComparison.InvariantCultureIgnoreCase));
                    if (await ConfirmationCodeService.GetConfirmationCode(validateCode) == account.id && LoginToken != null) {
                        return await ApplyValidatedPayload(loginForm["password"].ToString(), account);
                    }
                } else {
                    LoginToken = LoginService.Login(loginForm["email"].ToString(), loginForm["password"].ToString());
                    Account account = AccountService.Data.GetSingle(x => string.Equals(x.email, loginForm["email"].ToString(), StringComparison.InvariantCultureIgnoreCase));
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