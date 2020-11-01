using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Extensions;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class AccountsController : Controller {
        private readonly IAccountService accountService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly IDiscordService discordService;
        private readonly IDisplayNameService displayNameService;
        private readonly IHttpContextService httpContextService;
        private readonly IEmailService emailService;
        private readonly IRanksService ranksService;
        private readonly IRecruitmentService recruitmentService;

        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;
        private readonly ILogger logger;

        public AccountsController(
            IConfirmationCodeService confirmationCodeService,
            IRanksService ranksService,
            IAccountService accountService,
            IDisplayNameService displayNameService,
            IHttpContextService httpContextService,
            IRecruitmentService recruitmentService,
            ITeamspeakService teamspeakService,
            IEmailService emailService,
            IDiscordService discordService,
            IUnitsService unitsService,
            ILogger logger
        ) {
            this.confirmationCodeService = confirmationCodeService;
            this.ranksService = ranksService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.httpContextService = httpContextService;
            this.recruitmentService = recruitmentService;
            this.teamspeakService = teamspeakService;
            this.emailService = emailService;
            this.discordService = discordService;
            this.unitsService = unitsService;
            this.logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult Get() {
            Account account = accountService.GetUserAccount();
            return Ok(PubliciseAccount(account));
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult GetById(string id) {
            Account account = accountService.Data.GetSingle(id);
            return Ok(PubliciseAccount(account));
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] JObject body) {
            string email = body["email"].ToString();
            if (accountService.Data.Get(x => string.Equals(x.email, email, StringComparison.InvariantCultureIgnoreCase)).Any()) {
                return BadRequest(new { error = "an account with this email or username exists" });
            }

            Account account = new Account {
                email = email,
                password = BCrypt.Net.BCrypt.HashPassword(body["password"].ToString()),
                firstname = body["firstname"].ToString().ToTitleCase(),
                lastname = body["lastname"].ToString().ToTitleCase(),
                dob = DateTime.ParseExact($"{body["dobGroup"]["year"]}-{body["dobGroup"]["month"]}-{body["dobGroup"]["day"]}", "yyyy-M-d", CultureInfo.InvariantCulture),
                nation = body["nation"].ToString(),
                membershipState = MembershipState.UNCONFIRMED
            };
            await accountService.Data.Add(account);
            await SendConfirmationCode(account);
            logger.LogAudit($"New account created: '{account.firstname} {account.lastname}, {account.email}'", accountService.Data.GetSingle(x => x.email == account.email).id);
            return Ok(new { account.email });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> ApplyConfirmationCode([FromBody] JObject body) {
            string email = body.GetValueFromBody("email");
            string code = body.GetValueFromBody("code");

            try {
                GuardUtilites.ValidateString(email, _ => throw new ArgumentException($"Email '{email}' is invalid. Please refresh the page."));
                GuardUtilites.ValidateId(code, _ => throw new ArgumentException($"Code '{code}' is invalid. Please try again"));
            } catch (ArgumentException exception) {
                return BadRequest(new { error = exception.Message });
            }

            Account account = accountService.Data.GetSingle(x => x.email == email);
            if (account == null) {
                return BadRequest(new { error = $"An account with the email '{email}' doesn't exist. This should be impossible so please contact an admin for help" });
            }

            string value = await confirmationCodeService.GetConfirmationCode(code);
            if (value == email) {
                await accountService.Data.Update(account.id, "membershipState", MembershipState.CONFIRMED);
                logger.LogAudit($"Email address confirmed for {account.id}");
                return Ok();
            }

            await confirmationCodeService.ClearConfirmationCodes(x => x.value == email);
            await SendConfirmationCode(account);
            return BadRequest(new { error = $"The confirmation code was invalid or expired. A new code has been sent to '{account.email}'" });
        }

        [HttpPost("resend-email-code"), Authorize]
        public async Task<IActionResult> ResendConfirmationCode() {
            Account account = accountService.GetUserAccount();

            if (account.membershipState != MembershipState.UNCONFIRMED) {
                return BadRequest(new { error = "Account email has already been confirmed" });
            }

            await confirmationCodeService.ClearConfirmationCodes(x => x.value == account.email);
            await SendConfirmationCode(account);
            return Ok(PubliciseAccount(account));
        }

        [HttpGet("under"), Authorize(Roles = Permissions.COMMAND)]
        public IActionResult GetAccountsUnder([FromQuery] bool reverse = false) {
            List<object> accounts = new List<object>();

            List<Account> memberAccounts = accountService.Data.Get(x => x.membershipState == MembershipState.MEMBER).ToList();
            memberAccounts = memberAccounts.OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname).ThenBy(x => x.firstname).ToList();
            if (reverse) {
                memberAccounts.Reverse();
            }

            accounts.AddRange(memberAccounts.Select(x => new { value = x.id, displayValue = displayNameService.GetDisplayName(x) }));

            return Ok(accounts);
        }

        [HttpGet("roster"), Authorize]
        public IEnumerable<RosterAccount> GetRosterAccounts() {
            IEnumerable<Account> accounts = accountService.Data.Get(x => x.membershipState == MembershipState.MEMBER);
            IEnumerable<RosterAccount> accountObjects = accounts.OrderBy(x => x.rank, new RankComparer(ranksService))
                                                                .ThenBy(x => x.lastname)
                                                                .ThenBy(x => x.firstname)
                                                                .Select(x => new RosterAccount { id = x.id, nation = x.nation, rank = x.rank, roleAssignment = x.roleAssignment, unitAssignment = x.unitAssignment, name = $"{x.lastname}, {x.firstname}" });
            return accountObjects;
        }

        [HttpGet("online")]
        public IActionResult GetOnlineAccounts() {
            IEnumerable<TeamspeakClient> teamnspeakClients = teamspeakService.GetOnlineTeamspeakClients();
            IEnumerable<Account> allAccounts = accountService.Data.Get();
            var clients = teamnspeakClients.Where(x => x != null)
                                           .Select(
                                               x => new {
                                                   account = allAccounts.FirstOrDefault(y => y.teamspeakIdentities != null && y.teamspeakIdentities.Any(z => z.Equals(x.clientDbId))), client = x
                                               }
                                           )
                                           .ToList();
            var clientAccounts = clients.Where(x => x.account != null && x.account.membershipState == MembershipState.MEMBER)
                                        .OrderBy(x => x.account.rank, new RankComparer(ranksService))
                                        .ThenBy(x => x.account.lastname)
                                        .ThenBy(x => x.account.firstname);
            List<string> commandAccounts = unitsService.GetAuxilliaryRoot().members;

            List<object> commanders = new List<object>();
            List<object> recruiters = new List<object>();
            List<object> members = new List<object>();
            List<object> guests = new List<object>();
            foreach (var onlineClient in clientAccounts) {
                if (commandAccounts.Contains(onlineClient.account.id)) {
                    commanders.Add(new { displayName = displayNameService.GetDisplayName(onlineClient.account) });
                } else if (recruitmentService.IsRecruiter(onlineClient.account)) {
                    recruiters.Add(new { displayName = displayNameService.GetDisplayName(onlineClient.account) });
                } else {
                    members.Add(new { displayName = displayNameService.GetDisplayName(onlineClient.account) });
                }
            }

            foreach (var client in clients.Where(x => x.account == null || x.account.membershipState != MembershipState.MEMBER)) {
                guests.Add(new { displayName = client.client.clientName });
            }

            return Ok(new { commanders, recruiters, members, guests });
        }

        [HttpGet("exists")]
        public IActionResult CheckUsernameOrEmailExists([FromQuery] string check) {
            return Ok(accountService.Data.Get().Any(x => string.Equals(x.email, check, StringComparison.InvariantCultureIgnoreCase)) ? new { exists = true } : new { exists = false });
        }

        [HttpPut("name"), Authorize]
        public async Task<IActionResult> ChangeName([FromBody] JObject changeNameRequest) {
            Account account = accountService.GetUserAccount();
            await accountService.Data.Update(
                account.id,
                Builders<Account>.Update.Set(x => x.firstname, changeNameRequest["firstname"].ToString()).Set(x => x.lastname, changeNameRequest["lastname"].ToString())
            );
            logger.LogAudit($"{account.lastname}, {account.firstname} changed their name to {changeNameRequest["lastname"]}, {changeNameRequest["firstname"]}");
            await discordService.UpdateAccount(accountService.Data.GetSingle(account.id));
            return Ok();
        }

        [HttpPut("password"), Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] JObject changePasswordRequest) {
            string contextId = httpContextService.GetUserId();
            await accountService.Data.Update(contextId, "password", BCrypt.Net.BCrypt.HashPassword(changePasswordRequest["password"].ToString()));
            logger.LogAudit($"Password changed for {contextId}");
            return Ok();
        }

        [HttpPost("updatesetting/{id}"), Authorize]
        public async Task<IActionResult> UpdateSetting(string id, [FromBody] JObject body) {
            Account account = string.IsNullOrEmpty(id) ? accountService.GetUserAccount() : accountService.Data.GetSingle(id);
            await accountService.Data.Update(account.id, $"settings.{body["name"]}", body["value"]);
            logger.LogAudit($"Setting {body["name"]} updated for {account.id} from {account.settings.GetAttribute<bool>(body["name"].ToString())} to {body["value"]}");
            return Ok();
        }

        [HttpGet("test")]
        public IActionResult Test() {
            logger.LogInfo("This is a test");
            return Ok(new { value = DateTime.Now.ToLongTimeString() });
        }

        private PublicAccount PubliciseAccount(Account account) {
            PublicAccount publicAccount = account.ToPublicAccount();
            publicAccount.displayName = displayNameService.GetDisplayName(account);
            return publicAccount;
        }

        private async Task SendConfirmationCode(Account account) {
            string code = await confirmationCodeService.CreateConfirmationCode(account.email);
            string htmlContent =
                $"<strong>Your email was given for an application to join UKSF<br>Copy this code to your clipboard and return to the UKSF website application page to enter the code:<br><h3>{code}<h3></strong><br><p>If this request was not made by you, please contact an admin</p>";
            emailService.SendEmail(account.email, "UKSF Email Confirmation", htmlContent);
        }
    }
}
