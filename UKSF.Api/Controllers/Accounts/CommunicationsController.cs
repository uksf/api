using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Message;
using UKSF.Common;

namespace UKSF.Api.Controllers.Accounts {
    [Route("[controller]")]
    public class CommunicationsController : Controller {
        private readonly IAccountService accountService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly INotificationsService notificationsService;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;

        public CommunicationsController(IConfirmationCodeService confirmationCodeService, IAccountService accountService, ISessionService sessionService, ITeamspeakService teamspeakService, INotificationsService notificationsService) {
            this.confirmationCodeService = confirmationCodeService;
            this.accountService = accountService;
            this.sessionService = sessionService;
            this.teamspeakService = teamspeakService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IActionResult GetTeamspeakStatus() => Ok(new {isConnected = sessionService.GetContextAccount().teamspeakIdentities?.Count > 0});

        [HttpPost("send"), Authorize]
        public async Task<IActionResult> SendCode([FromBody] JObject body) {
            string mode = body["mode"].ToString();
            return mode switch {
                "teamspeak" => await SendTeamspeakCode(body["data"].ToString()),
                _ => BadRequest(new {error = $"Code mode '{mode}' not recognized"})
            };
        }

        [HttpPost("receive"), Authorize]
        public async Task<IActionResult> ReceiveCode([FromBody] JObject body) {
            string mode = body["mode"].ToString();
            string id = body["id"].ToString();
            string code = body["code"].ToString();
            string[] data = body["data"].ToString().Split(',');
            return mode switch {
                "teamspeak" => await ReceiveTeamspeakCode(id, code, data[0]),
                _ => BadRequest(new {error = $"Code mode '{mode}' not recognized"})
            };
        }

        private async Task<IActionResult> SendTeamspeakCode(string teamspeakDbId) {
            string code = await confirmationCodeService.CreateConfirmationCode(teamspeakDbId);
            await notificationsService.SendTeamspeakNotification(
                new HashSet<double> {teamspeakDbId.ToDouble()},
                $"This Teamspeak ID was selected for connection to the website. Copy this code to your clipboard and return to the UKSF website application page to enter the code:\n{code}\nIf this request was not made by you, please contact an admin"
            );
            return Ok();
        }

        private async Task<IActionResult> ReceiveTeamspeakCode(string id, string code, string checkId) {
            Account account = accountService.Data().GetSingle(id);
            string teamspeakId = await confirmationCodeService.GetConfirmationCode(code);
            if (string.IsNullOrWhiteSpace(teamspeakId) || teamspeakId != checkId) {
                return BadRequest(new {error = "The confirmation code has expired or is invalid. Please try again"});
            }

            if (account.teamspeakIdentities == null) account.teamspeakIdentities = new HashSet<double>();
            account.teamspeakIdentities.Add(double.Parse(teamspeakId));
            await accountService.Data().Update(account.id, Builders<Account>.Update.Set("teamspeakIdentities", account.teamspeakIdentities));
            account = accountService.Data().GetSingle(account.id);
            await teamspeakService.UpdateAccountTeamspeakGroups(account);
            await notificationsService.SendTeamspeakNotification(new HashSet<double> {teamspeakId.ToDouble()}, $"This teamspeak identity has been linked to the account with email '{account.email}'\nIf this was not done by you, please contact an admin");
            LogWrapper.AuditLog(account.id, $"Teamspeak ID {teamspeakId} added for {account.id}");
            return Ok();
        }
    }
}