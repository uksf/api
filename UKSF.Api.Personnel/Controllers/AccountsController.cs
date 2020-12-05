using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Extensions;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class AccountsController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IEventBus _eventBus;
        private readonly IAccountService _accountService;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IDisplayNameService _displayNameService;
        private readonly IEmailService _emailService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;
        private readonly IRanksService _ranksService;

        public AccountsController(
            IAccountContext accountContext,
            IConfirmationCodeService confirmationCodeService,
            IRanksService ranksService,
            IAccountService accountService,
            IDisplayNameService displayNameService,
            IHttpContextService httpContextService,
            IEmailService emailService,
            IEventBus eventBus,
            ILogger logger
        ) {
            _accountContext = accountContext;
            _confirmationCodeService = confirmationCodeService;
            _ranksService = ranksService;
            _accountService = accountService;
            _displayNameService = displayNameService;
            _httpContextService = httpContextService;
            _emailService = emailService;
            _eventBus = eventBus;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult Get() {
            Account account = _accountService.GetUserAccount();
            return Ok(PubliciseAccount(account));
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult GetById(string id) {
            Account account = _accountContext.GetSingle(id);
            return Ok(PubliciseAccount(account));
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] JObject body) {
            string email = body["email"].ToString();
            if (_accountContext.Get(x => string.Equals(x.Email, email, StringComparison.InvariantCultureIgnoreCase)).Any()) {
                return BadRequest(new { error = "an account with this email or username exists" });
            }

            Account account = new() {
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(body["password"].ToString()),
                Firstname = body["firstname"].ToString().ToTitleCase(),
                Lastname = body["lastname"].ToString().ToTitleCase(),
                Dob = DateTime.ParseExact($"{body["dobGroup"]["year"]}-{body["dobGroup"]["month"]}-{body["dobGroup"]["day"]}", "yyyy-M-d", CultureInfo.InvariantCulture),
                Nation = body["nation"].ToString(),
                MembershipState = MembershipState.UNCONFIRMED
            };
            await _accountContext.Add(account);
            await SendConfirmationCode(account);
            _logger.LogAudit($"New account created: '{account.Firstname} {account.Lastname}, {account.Email}'", _accountContext.GetSingle(x => x.Email == account.Email).Id);
            return Ok(new { email = account.Email });
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

            Account account = _accountContext.GetSingle(x => x.Email == email);
            if (account == null) {
                return BadRequest(new { error = $"An account with the email '{email}' doesn't exist. This should be impossible so please contact an admin for help" });
            }

            string value = await _confirmationCodeService.GetConfirmationCode(code);
            if (value == email) {
                await _accountContext.Update(account.Id, "membershipState", MembershipState.CONFIRMED);
                _logger.LogAudit($"Email address confirmed for {account.Id}");
                return Ok();
            }

            await _confirmationCodeService.ClearConfirmationCodes(x => x.Value == email);
            await SendConfirmationCode(account);
            return BadRequest(new { error = $"The confirmation code was invalid or expired. A new code has been sent to '{account.Email}'" });
        }

        [HttpPost("resend-email-code"), Authorize]
        public async Task<IActionResult> ResendConfirmationCode() {
            Account account = _accountService.GetUserAccount();

            if (account.MembershipState != MembershipState.UNCONFIRMED) {
                return BadRequest(new { error = "Account email has already been confirmed" });
            }

            await _confirmationCodeService.ClearConfirmationCodes(x => x.Value == account.Email);
            await SendConfirmationCode(account);
            return Ok(PubliciseAccount(account));
        }

        [HttpGet("under"), Authorize(Roles = Permissions.COMMAND)]
        public IActionResult GetAccountsUnder([FromQuery] bool reverse = false) {
            List<object> accounts = new();

            List<Account> memberAccounts = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER).ToList();
            memberAccounts = memberAccounts.OrderBy(x => x.Rank, new RankComparer(_ranksService)).ThenBy(x => x.Lastname).ThenBy(x => x.Firstname).ToList();
            if (reverse) {
                memberAccounts.Reverse();
            }

            accounts.AddRange(memberAccounts.Select(x => new { value = x.Id, displayValue = _displayNameService.GetDisplayName(x) }));

            return Ok(accounts);
        }

        [HttpGet("roster"), Authorize]
        public IEnumerable<RosterAccount> GetRosterAccounts() {
            IEnumerable<Account> accounts = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER);
            IEnumerable<RosterAccount> accountObjects = accounts.OrderBy(x => x.Rank, new RankComparer(_ranksService))
                                                                .ThenBy(x => x.Lastname)
                                                                .ThenBy(x => x.Firstname)
                                                                .Select(
                                                                    x => new RosterAccount {
                                                                        Id = x.Id,
                                                                        Nation = x.Nation,
                                                                        Rank = x.Rank,
                                                                        RoleAssignment = x.RoleAssignment,
                                                                        UnitAssignment = x.UnitAssignment,
                                                                        Name = $"{x.Lastname}, {x.Firstname}"
                                                                    }
                                                                );
            return accountObjects;
        }

        [HttpGet("exists")]
        public IActionResult CheckUsernameOrEmailExists([FromQuery] string check) {
            return Ok(_accountContext.Get().Any(x => string.Equals(x.Email, check, StringComparison.InvariantCultureIgnoreCase)) ? new { exists = true } : new { exists = false });
        }

        [HttpPut("name"), Authorize]
        public async Task<IActionResult> ChangeName([FromBody] JObject changeNameRequest) {
            Account account = _accountService.GetUserAccount();
            await _accountContext.Update(
                account.Id,
                Builders<Account>.Update.Set(x => x.Firstname, changeNameRequest["firstname"].ToString()).Set(x => x.Lastname, changeNameRequest["lastname"].ToString())
            );
            _logger.LogAudit($"{account.Lastname}, {account.Firstname} changed their name to {changeNameRequest["lastname"]}, {changeNameRequest["firstname"]}");
            _eventBus.Send(_accountContext.GetSingle(account.Id));
            return Ok();
        }

        [HttpPut("password"), Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] JObject changePasswordRequest) {
            string contextId = _httpContextService.GetUserId();
            await _accountContext.Update(contextId, "password", BCrypt.Net.BCrypt.HashPassword(changePasswordRequest["password"].ToString()));
            _logger.LogAudit($"Password changed for {contextId}");
            return Ok();
        }

        [HttpPost("updatesetting/{id}"), Authorize]
        public async Task<IActionResult> UpdateSetting(string id, [FromBody] AccountSettings settings) {
            Account account = string.IsNullOrEmpty(id) ? _accountService.GetUserAccount() : _accountContext.GetSingle(id);
            await _accountContext.Update(account.Id, Builders<Account>.Update.Set(x => x.Settings, settings));
            _logger.LogAudit($"Account settings updated: {account.Settings.Changes(settings)}");
            return Ok();
        }

        [HttpGet("test")]
        public IActionResult Test() {
            _logger.LogInfo("This is a test");
            return Ok(new { value = DateTime.Now.ToLongTimeString() });
        }

        private PublicAccount PubliciseAccount(Account account) {
            PublicAccount publicAccount = account.ToPublicAccount();
            publicAccount.DisplayName = _displayNameService.GetDisplayName(account);
            return publicAccount;
        }

        private async Task SendConfirmationCode(Account account) {
            string code = await _confirmationCodeService.CreateConfirmationCode(account.Email);
            string htmlContent =
                $"<strong>Your email was given for an application to join UKSF<br>Copy this code to your clipboard and return to the UKSF website application page to enter the code:<br><h3>{code}<h3></strong><br><p>If this request was not made by you, please contact an admin</p>";
            _emailService.SendEmail(account.Email, "UKSF Email Confirmation", htmlContent);
        }
    }
}
