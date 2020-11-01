using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class CommunicationsController : Controller {
        private readonly IAccountService accountService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly INotificationsService notificationsService;
        private readonly ILogger logger;

        private readonly ITeamspeakService teamspeakService;

        public CommunicationsController(
            IConfirmationCodeService confirmationCodeService,
            IAccountService accountService,
            ITeamspeakService teamspeakService,
            INotificationsService notificationsService,
            ILogger logger
        ) {
            this.confirmationCodeService = confirmationCodeService;
            this.accountService = accountService;

            this.teamspeakService = teamspeakService;
            this.notificationsService = notificationsService;
            this.logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult GetTeamspeakStatus() => Ok(new { isConnected = accountService.GetUserAccount().teamspeakIdentities?.Count > 0 });

        [HttpPost("send"), Authorize]
        public async Task<IActionResult> SendCode([FromBody] JObject body) {
            string mode = body.GetValueFromBody("mode");
            string data = body.GetValueFromBody("data");

            try {
                GuardUtilites.ValidateString(mode, _ => throw new ArgumentException("Mode is invalid"));
                GuardUtilites.ValidateString(data, _ => throw new ArgumentException("Data is invalid"));
            } catch (ArgumentException exception) {
                return BadRequest(new { error = exception.Message });
            }

            return mode switch {
                "teamspeak" => await SendTeamspeakCode(data),
                _ => BadRequest(new { error = $"Mode '{mode}' not recognized" })
            };
        }

        [HttpPost("receive"), Authorize]
        public async Task<IActionResult> ReceiveCode([FromBody] JObject body) {
            string id = body.GetValueFromBody("id");
            string code = body.GetValueFromBody("code");
            string mode = body.GetValueFromBody("mode");
            string data = body.GetValueFromBody("data");
            string[] dataArray = data.Split(',');

            try {
                GuardUtilites.ValidateId(id, _ => throw new ArgumentException($"Id '{id}' is invalid"));
                GuardUtilites.ValidateId(code, _ => throw new ArgumentException($"Code '{code}' is invalid. Please try again"));
                GuardUtilites.ValidateString(mode, _ => throw new ArgumentException("Mode is invalid"));
                GuardUtilites.ValidateString(data, _ => throw new ArgumentException("Data is invalid"));
                GuardUtilites.ValidateArray(dataArray, x => x.Length > 0, x => true, () => throw new ArgumentException("Data array is empty"));
            } catch (ArgumentException exception) {
                return BadRequest(new { error = exception.Message });
            }

            return mode switch {
                "teamspeak" => await ReceiveTeamspeakCode(id, code, dataArray[0]),
                _ => BadRequest(new { error = $"Mode '{mode}' not recognized" })
            };
        }

        private async Task<IActionResult> SendTeamspeakCode(string teamspeakDbId) {
            string code = await confirmationCodeService.CreateConfirmationCode(teamspeakDbId);
            await notificationsService.SendTeamspeakNotification(
                new HashSet<double> { teamspeakDbId.ToDouble() },
                $"This Teamspeak ID was selected for connection to the website. Copy this code to your clipboard and return to the UKSF website application page to enter the code:\n{code}\nIf this request was not made by you, please contact an admin"
            );
            return Ok();
        }

        private async Task<IActionResult> ReceiveTeamspeakCode(string id, string code, string checkId) {
            Account account = accountService.Data.GetSingle(id);
            string teamspeakId = await confirmationCodeService.GetConfirmationCode(code);
            if (string.IsNullOrWhiteSpace(teamspeakId) || teamspeakId != checkId) {
                return BadRequest(new { error = "The confirmation code has expired or is invalid. Please try again" });
            }

            account.teamspeakIdentities ??= new HashSet<double>();
            account.teamspeakIdentities.Add(double.Parse(teamspeakId));
            await accountService.Data.Update(account.id, Builders<Account>.Update.Set("teamspeakIdentities", account.teamspeakIdentities));
            account = accountService.Data.GetSingle(account.id);
            await teamspeakService.UpdateAccountTeamspeakGroups(account);
            await notificationsService.SendTeamspeakNotification(
                new HashSet<double> { teamspeakId.ToDouble() },
                $"This teamspeak identity has been linked to the account with email '{account.email}'\nIf this was not done by you, please contact an admin"
            );
            logger.LogAudit($"Teamspeak ID {teamspeakId} added for {account.id}");
            return Ok();
        }
    }
}
