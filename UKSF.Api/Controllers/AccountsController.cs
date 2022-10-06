using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using NameCase;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Parameters;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Mappers;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Queries;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountMapper _accountMapper;
    private readonly IAccountService _accountService;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IEventBus _eventBus;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly IRanksService _ranksService;
    private readonly ISendTemplatedEmailCommand _sendTemplatedEmailCommand;

    public AccountsController(
        IAccountContext accountContext,
        IConfirmationCodeService confirmationCodeService,
        IRanksService ranksService,
        IAccountService accountService,
        IDisplayNameService displayNameService,
        IHttpContextService httpContextService,
        ISendTemplatedEmailCommand sendTemplatedEmailCommand,
        IEventBus eventBus,
        IUksfLogger logger,
        IAccountMapper accountMapper
    )
    {
        _accountContext = accountContext;
        _confirmationCodeService = confirmationCodeService;
        _ranksService = ranksService;
        _accountService = accountService;
        _displayNameService = displayNameService;
        _httpContextService = httpContextService;
        _sendTemplatedEmailCommand = sendTemplatedEmailCommand;
        _eventBus = eventBus;
        _logger = logger;
        _accountMapper = accountMapper;
    }

    [HttpGet]
    [Authorize]
    public Account Get()
    {
        var domainAccount = _accountService.GetUserAccount();
        return _accountMapper.MapToAccount(domainAccount);
    }

    [HttpGet("{id}")]
    [Authorize]
    public Account GetById([FromRoute] string id)
    {
        var domainAccount = _accountContext.GetSingle(id);
        return _accountMapper.MapToAccount(domainAccount);
    }

    [HttpPut]
    public async Task<Account> Put([FromBody] CreateAccount createAccount)
    {
        if (_accountContext.Get(x => string.Equals(x.Email, createAccount.Email, StringComparison.InvariantCultureIgnoreCase)).Any())
        {
            throw new AccountAlreadyExistsException();
        }

        DomainAccount domainAccount = new()
        {
            Email = createAccount.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(createAccount.Password),
            Firstname = createAccount.FirstName.Trim().ToTitleCase(),
            Lastname = createAccount.LastName.Trim().ToNameCase(),
            Dob = DateTime.ParseExact($"{createAccount.DobYear}-{createAccount.DobMonth}-{createAccount.DobDay}", "yyyy-M-d", CultureInfo.InvariantCulture),
            Nation = createAccount.Nation,
            MembershipState = MembershipState.UNCONFIRMED
        };
        await _accountContext.Add(domainAccount);
        await SendConfirmationCode(domainAccount);

        var createdAccount = _accountContext.GetSingle(x => x.Email == domainAccount.Email);
        _logger.LogAudit($"New account created: '{domainAccount.Firstname} {domainAccount.Lastname}, {domainAccount.Email}'", createdAccount.Id);

        return _accountMapper.MapToAccount(createdAccount);
    }

    [HttpPost]
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

        var domainAccount = _accountContext.GetSingle(x => x.Email == email);
        if (domainAccount == null)
        {
            throw new BadRequestException($"An account with the email '{email}' doesn't exist. This should be impossible so please contact an admin for help");
        }

        var value = await _confirmationCodeService.GetConfirmationCodeValue(code);
        if (value == email)
        {
            await _accountContext.Update(domainAccount.Id, x => x.MembershipState, MembershipState.CONFIRMED);
            _logger.LogAudit($"Email address confirmed for {domainAccount.Id}");
            return;
        }

        await _confirmationCodeService.ClearConfirmationCodes(x => x.Value == email);
        await SendConfirmationCode(domainAccount);
        throw new BadRequestException($"The confirmation code was invalid or expired. A new code has been sent to '{domainAccount.Email}'");
    }

    [HttpPost("resend-email-code")]
    [Authorize]
    public async Task<Account> ResendConfirmationCode()
    {
        var domainAccount = _accountService.GetUserAccount();

        if (domainAccount.MembershipState != MembershipState.UNCONFIRMED)
        {
            throw new AccountAlreadyConfirmedException();
        }

        await _confirmationCodeService.ClearConfirmationCodes(x => x.Value == domainAccount.Email);
        await SendConfirmationCode(domainAccount);
        return _accountMapper.MapToAccount(domainAccount);
    }

    [HttpGet("under")]
    [Permissions(Roles = Permissions.Command)]
    public List<BasicAccount> GetAccountsUnder([FromQuery] bool reverse = false)
    {
        var memberAccounts = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER).ToList();
        memberAccounts = memberAccounts.OrderBy(x => x.Rank, new RankComparer(_ranksService)).ThenBy(x => x.Lastname).ThenBy(x => x.Firstname).ToList();
        if (reverse)
        {
            memberAccounts.Reverse();
        }

        return memberAccounts.Select(x => new BasicAccount { Id = x.Id, DisplayName = _displayNameService.GetDisplayName(x) }).ToList();
    }

    [HttpGet("roster")]
    [Authorize]
    public IEnumerable<RosterAccount> GetRosterAccounts()
    {
        var accounts = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER);
        var accountObjects = accounts.OrderBy(x => x.Rank, new RankComparer(_ranksService))
                                     .ThenBy(x => x.Lastname)
                                     .ThenBy(x => x.Firstname)
                                     .Select(
                                         x => new RosterAccount
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
        return _accountContext.Get().Any(x => string.Equals(x.Email, check, StringComparison.InvariantCultureIgnoreCase));
    }

    [HttpPut("name")]
    [Authorize]
    public async Task ChangeName([FromBody] ChangeName changeName)
    {
        var domainAccount = _accountService.GetUserAccount();
        var firstName = changeName.FirstName.Trim().ToTitleCase();
        var lastName = changeName.LastName.Trim().ToNameCase();

        await _accountContext.Update(domainAccount.Id, Builders<DomainAccount>.Update.Set(x => x.Firstname, firstName).Set(x => x.Lastname, lastName));
        _logger.LogAudit($"{domainAccount.Lastname}, {domainAccount.Firstname} changed their name to {lastName}, {firstName}");
        _eventBus.Send(_accountContext.GetSingle(domainAccount.Id));
    }

    [HttpPut("password")]
    [Authorize]
    public async Task ChangePassword([FromBody] ChangePasswordRequest changePasswordRequest)
    {
        var contextId = _httpContextService.GetUserId();
        await _accountContext.Update(contextId, x => x.Password, BCrypt.Net.BCrypt.HashPassword(changePasswordRequest.Password));
        _logger.LogAudit($"Password changed for {contextId}");
    }

    [HttpPut("{id}/updatesetting")]
    [Authorize]
    public async Task<AccountSettings> UpdateSetting([FromRoute] string id, [FromBody] AccountSettings settings)
    {
        var domainAccount = string.IsNullOrEmpty(id) ? _accountService.GetUserAccount() : _accountContext.GetSingle(id);
        await _accountContext.Update(domainAccount.Id, Builders<DomainAccount>.Update.Set(x => x.Settings, settings));
        _logger.LogAudit($"Account settings updated: {domainAccount.Settings.Changes(settings)}");

        return _accountContext.GetSingle(domainAccount.Id).Settings;
    }

    [HttpGet("test")]
    public string Test()
    {
        _logger.LogInfo("This is a test");
        return DateTime.UtcNow.ToLongTimeString();
    }

    private async Task SendConfirmationCode(DomainAccount domainAccount)
    {
        var code = await _confirmationCodeService.CreateConfirmationCode(domainAccount.Email);
        await _sendTemplatedEmailCommand.ExecuteAsync(
            new(domainAccount.Email, "UKSF Account Confirmation", TemplatedEmailNames.AccountConfirmationTemplate, new() { { "code", code } })
        );
    }
}
