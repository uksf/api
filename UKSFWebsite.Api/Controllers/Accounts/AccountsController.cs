using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Personnel;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers.Accounts {
    [Route("[controller]")]
    public class AccountsController : Controller {
        private readonly IAccountService accountService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly IDiscordService discordService;
        private readonly IDisplayNameService displayNameService;
        private readonly IEmailService emailService;
        private readonly IRanksService ranksService;
        private readonly IRecruitmentService recruitmentService;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;

        public AccountsController(
            IConfirmationCodeService confirmationCodeService,
            IRanksService ranksService,
            IAccountService accountService,
            IDisplayNameService displayNameService,
            ISessionService sessionService,
            IRecruitmentService recruitmentService,
            ITeamspeakService teamspeakService,
            IEmailService emailService,
            IDiscordService discordService,
            IUnitsService unitsService
        ) {
            this.confirmationCodeService = confirmationCodeService;
            this.ranksService = ranksService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.sessionService = sessionService;
            this.recruitmentService = recruitmentService;
            this.teamspeakService = teamspeakService;
            this.emailService = emailService;
            this.discordService = discordService;
            this.unitsService = unitsService;
        }

        [HttpGet, Authorize]
        public IActionResult Get() {
            Account account = sessionService.GetContextAccount();
            return Ok(FormatAccount(account));
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult GetById(string id) {
            Account account = accountService.Data().GetSingle(id);
            return Ok(FormatAccount(account));
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] JObject body) {
            string email = body["email"].ToString();
            if (accountService.Data().Get(x => string.Equals(x.email, email, StringComparison.InvariantCultureIgnoreCase)).Any()) {
                return BadRequest(new {error = "an account with this email or username exists"});
            }

            Account account = new Account {
                email = email,
                password = BCrypt.Net.BCrypt.HashPassword(body["password"].ToString()),
                firstname = StringUtilities.ToTitleCase(body["firstname"].ToString()),
                lastname = StringUtilities.ToTitleCase(body["lastname"].ToString()),
                dob = DateTime.ParseExact($"{body["dobGroup"]["year"]}-{body["dobGroup"]["month"]}-{body["dobGroup"]["day"]}", "yyyy-M-d", CultureInfo.InvariantCulture),
                nation = body["nation"].ToString(),
                membershipState = MembershipState.UNCONFIRMED
            };
            await accountService.Data().Add(account);
            await SendConfirmationCode(account);
            LogWrapper.AuditLog(accountService.Data().GetSingle(x => x.email == account.email).id, $"New account created: '{account.firstname} {account.lastname}, {account.email}'");
            return Ok(new {account.email});
        }

        [HttpPost]
        public async Task<IActionResult> ApplyConfirmationCode([FromBody] JObject body) {
            string code = body["code"].ToString();
            string email = body["email"].ToString();
            Account account = accountService.Data().GetSingle(x => x.email == email);
            if (account == null) {
                return BadRequest(new {error = $"An account with the email '{email}' doesn't exist. This should be impossible so please contact an admin for help"});
            }

            string value = await confirmationCodeService.GetConfirmationCode(code);
            if (value == email) {
                await accountService.Data().Update(account.id, "membershipState", MembershipState.CONFIRMED);
                LogWrapper.AuditLog(account.id, $"Email address confirmed for {account.id}");
                return Ok();
            }

            await SendConfirmationCode(account);
            return BadRequest(new {error = $"The confirmation code has expired. A new code has been sent to '{account.email}'"});
        }

        [HttpGet("under"), Authorize(Roles = RoleDefinitions.COMMAND)]
        public IActionResult GetAccountsUnder([FromQuery] bool reverse = false) {
            List<object> accounts = new List<object>();

            List<Account> memberAccounts = accountService.Data().Get(x => x.membershipState == MembershipState.MEMBER).ToList();
            if (reverse) {
                memberAccounts.Sort((x, y) => ranksService.Sort(y.rank, x.rank));
            } else {
                memberAccounts.Sort((x, y) => ranksService.Sort(x.rank, y.rank));
            }

            accounts.AddRange(memberAccounts.Select(x => new {value = x.id, displayValue = displayNameService.GetDisplayName(x)}));

            return Ok(accounts);
        }

        [HttpGet("roster"), Authorize]
        public IActionResult GetRosterAccounts() {
            List<object> accountObjects = new List<object>();
            List<Account> accounts = accountService.Data().Get(x => x.membershipState == MembershipState.MEMBER);
            accounts = accounts.OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname).ThenBy(x => x.firstname).ToList();
            accountObjects.AddRange(
                accounts.Select(
                    document => new {
                        document.id,
                        document.nation,
                        document.rank,
                        document.roleAssignment,
                        document.unitAssignment,
                        name = $"{document.lastname}, {document.firstname}"
                    }
                )
            );
            return Ok(accountObjects);
        }

        [HttpGet("online")]
        public IActionResult GetOnlineAccounts() {
            HashSet<TeamspeakClient> teamnspeakClients = teamspeakService.GetOnlineTeamspeakClients();
            List<Account> allAccounts = accountService.Data().Get();
            var clients = teamnspeakClients.Where(x => x != null).Select(x => new {account = allAccounts.FirstOrDefault(y => y.teamspeakIdentities != null && y.teamspeakIdentities.Any(z => z.Equals(x.clientDbId))), client = x}).ToList();
            var clientAccounts = clients.Where(x => x.account != null && x.account.membershipState == MembershipState.MEMBER).OrderBy(x => x.account.rank, new RankComparer(ranksService)).ThenBy(x => x.account.lastname).ThenBy(x => x.account.firstname);
            List<string> commandAccounts = unitsService.GetAuxilliaryRoot().members;
            
            List<object> commanders = new List<object>();
            List<object> recruiters = new List<object>();
            List<object> members = new List<object>();
            List<object> guests = new List<object>();
            foreach (var onlineClient in clientAccounts) {
                if (commandAccounts.Contains(onlineClient.account.id)) {
                    commanders.Add(new {displayName = displayNameService.GetDisplayName(onlineClient.account)});
                } else if (recruitmentService.IsRecruiter(onlineClient.account)) {
                    recruiters.Add(new {displayName = displayNameService.GetDisplayName(onlineClient.account)});
                } else {
                    members.Add(new {displayName = displayNameService.GetDisplayName(onlineClient.account)});
                }
            }

            foreach (var client in clients.Where(x => x.account == null || x.account.membershipState != MembershipState.MEMBER)) {
                guests.Add(new {displayName = client.client.clientName});
            }

            return Ok(new {commanders, recruiters, members, guests});
        }

        [HttpGet("exists")]
        public IActionResult CheckUsernameOrEmailExists([FromQuery] string check) {
            return Ok(accountService.Data().Get().Any(x => string.Equals(x.email, check, StringComparison.InvariantCultureIgnoreCase)) ? new {exists = true} : new {exists = false});
        }

        [HttpPut("name"), Authorize]
        public async Task<IActionResult> ChangeName([FromBody] JObject changeNameRequest) {
            Account account = sessionService.GetContextAccount();
            await accountService.Data().Update(account.id, Builders<Account>.Update.Set(x => x.firstname, changeNameRequest["firstname"].ToString()).Set(x => x.lastname, changeNameRequest["lastname"].ToString()));
            LogWrapper.AuditLog(sessionService.GetContextId(), $"{account.lastname}, {account.firstname} changed their name to {changeNameRequest["lastname"]}, {changeNameRequest["firstname"]}");
            await discordService.UpdateAccount(accountService.Data().GetSingle(account.id));
            return Ok();
        }

        [HttpPut("password"), Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] JObject changePasswordRequest) {
            string contextId = sessionService.GetContextId();
            await accountService.Data().Update(contextId, "password", BCrypt.Net.BCrypt.HashPassword(changePasswordRequest["password"].ToString()));
            LogWrapper.AuditLog(contextId, $"Password changed for {contextId}");
            return Ok();
        }

        [HttpPost("updatesetting/{id}"), Authorize]
        public async Task<IActionResult> UpdateSetting(string id, [FromBody] JObject body) {
            Account account = string.IsNullOrEmpty(id) ? sessionService.GetContextAccount() : accountService.Data().GetSingle(id);
            await accountService.Data().Update(account.id, $"settings.{body["name"]}", body["value"]);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Setting {body["name"]} updated for {account.id} from {account.settings.GetAttribute<bool>(body["name"].ToString())} to {body["value"]}");
            return Ok();
        }

        [HttpGet("test")]
        public IActionResult Test() {
            LogWrapper.Log("This is a test");
            return Ok(new {value = DateTime.Now.ToLongTimeString()});
        }

        private dynamic FormatAccount(Account account) {
            dynamic responseAccount = account.ToDynamicAccount();
            responseAccount.displayName = displayNameService.GetDisplayName(account);
            responseAccount.sr1 = sessionService.ContextHasRole(RoleDefinitions.SR1);
            responseAccount.sr5 = sessionService.ContextHasRole(RoleDefinitions.SR5);
            responseAccount.sr10 = sessionService.ContextHasRole(RoleDefinitions.SR10);
            responseAccount.sr1Lead = sessionService.ContextHasRole(RoleDefinitions.SR1_LEAD);
            responseAccount.command = sessionService.ContextHasRole(RoleDefinitions.COMMAND);
            responseAccount.admin = sessionService.ContextHasRole(RoleDefinitions.ADMIN);
            responseAccount.nco = sessionService.ContextHasRole(RoleDefinitions.NCO);
            return responseAccount;
        }

        private async Task SendConfirmationCode(Account account) {
            string code = await confirmationCodeService.CreateConfirmationCode(account.email);
            string htmlContent = $"<strong>Your email was given for an application to join UKSF<br>Copy this code to your clipboard and return to the UKSF website application page to enter the code:<br><h3>{code}<h3></strong><br><p>If this request was not made by you, please contact an admin</p>";
            emailService.SendEmail(account.email, "UKSF Email Confirmation", htmlContent);
        }
    }
}
