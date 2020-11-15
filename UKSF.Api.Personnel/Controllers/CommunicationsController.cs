using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Controllers {
    // TODO: Needs to be renamed and singled out. Won't be any other communication connections to add
    [Route("[controller]")]
    public class CommunicationsController : Controller {
        private readonly IAccountService _accountService;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly ILogger _logger;
        private readonly IEventBus<Account> _accountEventBus;
        private readonly INotificationsService _notificationsService;

        public CommunicationsController(IConfirmationCodeService confirmationCodeService, IAccountService accountService, INotificationsService notificationsService, ILogger logger, IEventBus<Account> accountEventBus) {
            _confirmationCodeService = confirmationCodeService;
            _accountService = accountService;
            _notificationsService = notificationsService;
            _logger = logger;
            _accountEventBus = accountEventBus;
        }

        [HttpGet, Authorize]
        public IActionResult GetTeamspeakStatus() => Ok(new { isConnected = _accountService.GetUserAccount().teamspeakIdentities?.Count > 0 });

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
                _           => BadRequest(new { error = $"Mode '{mode}' not recognized" })
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
                GuardUtilites.ValidateArray(dataArray, x => x.Length > 0, _ => true, () => throw new ArgumentException("Data array is empty"));
            } catch (ArgumentException exception) {
                return BadRequest(new { error = exception.Message });
            }

            return mode switch {
                "teamspeak" => await ReceiveTeamspeakCode(id, code, dataArray[0]),
                _           => BadRequest(new { error = $"Mode '{mode}' not recognized" })
            };
        }

        private async Task<IActionResult> SendTeamspeakCode(string teamspeakDbId) {
            string code = await _confirmationCodeService.CreateConfirmationCode(teamspeakDbId);
            _notificationsService.SendTeamspeakNotification(
                new HashSet<double> { teamspeakDbId.ToDouble() },
                $"This Teamspeak ID was selected for connection to the website. Copy this code to your clipboard and return to the UKSF website application page to enter the code:\n{code}\nIf this request was not made by you, please contact an admin"
            );
            return Ok();
        }

        private async Task<IActionResult> ReceiveTeamspeakCode(string id, string code, string checkId) {
            Account account = _accountService.Data.GetSingle(id);
            string teamspeakId = await _confirmationCodeService.GetConfirmationCode(code);
            if (string.IsNullOrWhiteSpace(teamspeakId) || teamspeakId != checkId) {
                return BadRequest(new { error = "The confirmation code has expired or is invalid. Please try again" });
            }

            account.teamspeakIdentities ??= new HashSet<double>();
            account.teamspeakIdentities.Add(double.Parse(teamspeakId));
            await _accountService.Data.Update(account.id, Builders<Account>.Update.Set("teamspeakIdentities", account.teamspeakIdentities));
            account = _accountService.Data.GetSingle(account.id);
            _accountEventBus.Send(account);
            _notificationsService.SendTeamspeakNotification(
                new HashSet<double> { teamspeakId.ToDouble() },
                $"This teamspeak identity has been linked to the account with email '{account.email}'\nIf this was not done by you, please contact an admin"
            );
            _logger.LogAudit($"Teamspeak ID {teamspeakId} added for {account.id}");
            return Ok();
        }
    }
}
