using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using NameCase;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Queries;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class AccountsController(
    IAccountContext accountContext,
    IConfirmationCodeService confirmationCodeService,
    IRanksService ranksService,
    IAccountService accountService,
    IDisplayNameService displayNameService,
    IHttpContextService httpContextService,
    ISendTemplatedEmailCommand sendTemplatedEmailCommand,
    IAccountMapper accountMapper,
    ILoginService loginService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public Account Get()
    {
        var account = accountService.GetUserAccount();
        return accountMapper.MapToAccount(account);
    }

    [HttpGet("{id}")]
    [Authorize]
    public Account GetById([FromRoute] string id)
    {
        var account = accountContext.GetSingle(id);
        return accountMapper.MapToAccount(account);
    }

    [HttpPost("create")]
    public async Task<TokenResponse> Create([FromBody] CreateAccount createAccount)
    {
        if (accountContext.Get(x => string.Equals(x.Email, createAccount.Email, StringComparison.InvariantCultureIgnoreCase)).Any())
        {
            throw new AccountAlreadyExistsException();
        }

        DomainAccount account = new()
        {
            Email = createAccount.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(createAccount.Password),
            Firstname = createAccount.FirstName.Trim().ToTitleCase(),
            Lastname = createAccount.LastName.Trim().ToNameCase(),
            Dob = DateTime.ParseExact($"{createAccount.DobYear}-{createAccount.DobMonth}-{createAccount.DobDay}", "yyyy-M-d", CultureInfo.InvariantCulture),
            Nation = createAccount.Nation,
            MembershipState = MembershipState.Unconfirmed
        };
        await accountContext.Add(account);
        await SendConfirmationCode(account);

        var createdAccount = accountContext.GetSingle(x => x.Email == account.Email);
        logger.LogAudit($"New account created: '{account.Firstname} {account.Lastname}, {account.Email}'", createdAccount.Id);

        return loginService.Login(createAccount.Email, createAccount.Password);
    }

    [HttpPost("code")]
    [Authorize]
    public async Task ApplyConfirmationCode([FromBody] ApplyConfirmationCodeRequest applyConfirmationCodeRequest)
    {
        var email = applyConfirmationCodeRequest.Email;
        var code = applyConfirmationCodeRequest.Code;

        try
        {
            GuardUtilites.ValidateString(email, _ => throw new ArgumentException($"Email '{email}' is invalid. Please refresh the page."));
            GuardUtilites.ValidateId(code, _ => throw new ArgumentException($"Code '{code}' is invalid. Please try again"));
        }
        catch (ArgumentException exception)
        {
            throw new BadRequestException(exception.Message);
        }

        var account = accountContext.GetSingle(x => x.Email == email);
        if (account == null)
        {
            throw new BadRequestException($"An account with the email '{email}' doesn't exist. This should be impossible so please contact an admin for help");
        }

        var value = await confirmationCodeService.GetConfirmationCodeValue(code);
        if (value == email)
        {
            await accountContext.Update(account.Id, x => x.MembershipState, MembershipState.Confirmed);
            logger.LogAudit($"Email address confirmed for {account.Id}");
            return;
        }

        await confirmationCodeService.ClearConfirmationCodes(x => x.Value == email);
        await SendConfirmationCode(account);
        throw new BadRequestException($"The confirmation code was invalid or expired. A new code has been sent to '{account.Email}'");
    }

    [HttpPost("resend-email-code")]
    [Authorize]
    public async Task ResendConfirmationCode()
    {
        var account = accountService.GetUserAccount();
        if (account.MembershipState != MembershipState.Unconfirmed)
        {
            throw new AccountAlreadyConfirmedException();
        }

        await confirmationCodeService.ClearConfirmationCodes(x => x.Value == account.Email);
        await SendConfirmationCode(account);
    }

    [HttpGet("members")]
    [Permissions(Roles = Permissions.Command)]
    public List<BasicAccount> GetMemberAccounts([FromQuery] bool reverse = false)
    {
        var memberAccounts = accountContext.Get(x => x.IsMember()).ToList();
        memberAccounts = memberAccounts.OrderBy(x => x.Rank, new RankComparer(ranksService)).ThenBy(x => x.Lastname).ThenBy(x => x.Firstname).ToList();
        if (reverse)
        {
            memberAccounts.Reverse();
        }

        return memberAccounts.Select(x => new BasicAccount { Id = x.Id, DisplayName = displayNameService.GetDisplayName(x) }).ToList();
    }

    [HttpGet("active")]
    [Permissions(Roles = Permissions.Command)]
    public List<BasicAccount> GetActiveAccounts([FromQuery] bool reverse = false)
    {
        var memberAccounts = accountContext.Get(x => x.IsMember() || x.IsCandidate()).ToList();
        memberAccounts = memberAccounts.OrderBy(x => x.Rank, new RankComparer(ranksService)).ThenBy(x => x.Lastname).ThenBy(x => x.Firstname).ToList();
        if (reverse)
        {
            memberAccounts.Reverse();
        }

        return memberAccounts.Select(x => new BasicAccount { Id = x.Id, DisplayName = displayNameService.GetDisplayName(x) }).ToList();
    }

    [HttpGet("roster")]
    [Authorize]
    public IEnumerable<RosterAccount> GetRosterAccounts()
    {
        var accounts = accountContext.Get(x => x.IsMember());
        var accountObjects = accounts.OrderBy(x => x.Rank, new RankComparer(ranksService))
                                     .ThenBy(x => x.Lastname)
                                     .ThenBy(x => x.Firstname)
                                     .Select(x => new RosterAccount
                                         {
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
    public bool CheckUsernameOrEmailExists([FromQuery] string check)
    {
        return accountContext.Get().Any(x => string.Equals(x.Email, check, StringComparison.InvariantCultureIgnoreCase));
    }

    [HttpPut("name")]
    [Authorize]
    public async Task ChangeName([FromBody] ChangeName changeName)
    {
        var account = accountService.GetUserAccount();
        var firstName = changeName.FirstName.Trim().ToTitleCase();
        var lastName = changeName.LastName.Trim().ToNameCase();

        await accountContext.Update(account.Id, Builders<DomainAccount>.Update.Set(x => x.Firstname, firstName).Set(x => x.Lastname, lastName));
        logger.LogAudit($"{account.Lastname}, {account.Firstname} changed their name to {lastName}, {firstName}");
    }

    [HttpPut("password")]
    [Authorize]
    public async Task ChangePassword([FromBody] ChangePasswordRequest changePasswordRequest)
    {
        var contextId = httpContextService.GetUserId();
        await accountContext.Update(contextId, x => x.Password, BCrypt.Net.BCrypt.HashPassword(changePasswordRequest.Password));
        logger.LogAudit($"Password changed for {contextId}");
    }

    [HttpPut("{id}/updatesetting")]
    [Authorize]
    public async Task<AccountSettings> UpdateSetting([FromRoute] string id, [FromBody] AccountSettings settings)
    {
        var account = string.IsNullOrEmpty(id) ? accountService.GetUserAccount() : accountContext.GetSingle(id);
        await accountContext.Update(account.Id, Builders<DomainAccount>.Update.Set(x => x.Settings, settings));
        logger.LogAudit($"Account settings updated: {account.Settings.Changes(settings)}");

        return accountContext.GetSingle(account.Id).Settings;
    }

    [HttpGet("test")]
    public string Test()
    {
        logger.LogInfo("This is a test");
        return DateTime.UtcNow.ToLongTimeString();
    }

    private async Task SendConfirmationCode(DomainAccount account)
    {
        var code = await confirmationCodeService.CreateConfirmationCode(account.Email);
        await sendTemplatedEmailCommand.ExecuteAsync(
            new SendTemplatedEmailCommandArgs(
                account.Email,
                "UKSF Account Confirmation",
                TemplatedEmailNames.AccountConfirmationTemplate,
                new Dictionary<string, string> { { "code", code } }
            )
        );
    }
}
