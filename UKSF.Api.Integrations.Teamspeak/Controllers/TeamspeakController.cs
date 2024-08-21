using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.Controllers;

[Route("teamspeak")]
public class TeamspeakController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IRanksService _ranksService;
    private readonly IRecruitmentService _recruitmentService;
    private readonly ITeamspeakService _teamspeakService;
    private readonly IUnitsService _unitsService;

    public TeamspeakController(
        IAccountContext accountContext,
        ITeamspeakService teamspeakService,
        IRanksService ranksService,
        IUnitsService unitsService,
        IRecruitmentService recruitmentService,
        IDisplayNameService displayNameService,
        IConfirmationCodeService confirmationCodeService,
        INotificationsService notificationsService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _teamspeakService = teamspeakService;
        _ranksService = ranksService;
        _unitsService = unitsService;
        _recruitmentService = recruitmentService;
        _displayNameService = displayNameService;
        _confirmationCodeService = confirmationCodeService;
        _notificationsService = notificationsService;
        _logger = logger;
    }

    [HttpGet("{teamspeakId}")]
    [Authorize]
    public async Task RequestTeamspeakCode([FromRoute] string teamspeakId)
    {
        var code = await _confirmationCodeService.CreateConfirmationCode(teamspeakId);

        _notificationsService.SendTeamspeakNotification(
            new HashSet<int> { teamspeakId.ToInt() },
            $"This Teamspeak client was selected for connection to the website. Enter this code in the website to complete the connection:\n{code}" +
            $"\nIf this request was not made by you, it is safe to ignore. Do not pass this code on to anyone else."
        );

        var teamspeakClientName = _teamspeakService.GetOnlineTeamspeakClients().FirstOrDefault(x => x.ClientDbId.ToString() == teamspeakId)?.ClientName;
        _logger.LogAudit($"Teamspeak connection code requested for Teamspeak client '{teamspeakClientName}'");
    }

    [HttpGet("online")]
    [Permissions(Permissions.Confirmed, Permissions.Member, Permissions.Discharged)]
    public List<TeamspeakConnectClient> GetOnlineClients()
    {
        return _teamspeakService.GetFormattedClients();
    }

    [HttpGet("reload")]
    [Permissions(Permissions.Admin)]
    public async Task Reload()
    {
        _logger.LogInfo("Teampseak reload via API");
        await _teamspeakService.Reload();
    }

    [HttpGet("shutdown")]
    [Permissions(Permissions.Admin)]
    public async Task Shutdown()
    {
        _logger.LogInfo("Teampseak shutdown via API");
        await _teamspeakService.Shutdown();
        await Task.Delay(TimeSpan.FromSeconds(3));
    }

    [HttpGet("onlineAccounts")]
    public TeamspeakAccountsDataset GetOnlineAccounts()
    {
        var teamspeakClients = _teamspeakService.GetOnlineTeamspeakClients().ToList();
        var allAccounts = _accountContext.Get().ToList();
        var clients = teamspeakClients.Where(x => x != null)
                                      .Select(
                                          client =>
                                          {
                                              var account = allAccounts.FirstOrDefault(
                                                  x => x.TeamspeakIdentities != null && x.TeamspeakIdentities.Any(tsDbId => tsDbId == client.ClientDbId)
                                              );
                                              return new { account, client };
                                          }
                                      )
                                      .ToList();
        var clientAccounts = clients.Where(x => x.account is { MembershipState: MembershipState.Member })
                                    .OrderBy(x => x.account.Rank, new RankComparer(_ranksService))
                                    .ThenBy(x => x.account.Lastname)
                                    .ThenBy(x => x.account.Firstname)
                                    .ToList();
        var commandAccounts = _unitsService.GetAuxiliaryRoot().Members;

        List<TeamspeakAccountDataset> commanders = new();
        List<TeamspeakAccountDataset> recruiters = new();
        List<TeamspeakAccountDataset> members = new();
        foreach (var onlineClient in clientAccounts)
        {
            if (commandAccounts.Contains(onlineClient.account.Id))
            {
                commanders.Add(new TeamspeakAccountDataset { DisplayName = _displayNameService.GetDisplayName(onlineClient.account) });
            }
            else if (_recruitmentService.IsRecruiter(onlineClient.account))
            {
                recruiters.Add(new TeamspeakAccountDataset { DisplayName = _displayNameService.GetDisplayName(onlineClient.account) });
            }
            else
            {
                members.Add(new TeamspeakAccountDataset { DisplayName = _displayNameService.GetDisplayName(onlineClient.account) });
            }
        }

        var guests = clients.Where(x => x.account is not { MembershipState: MembershipState.Member })
                            .Select(client => new TeamspeakAccountDataset { DisplayName = client.client.ClientName })
                            .ToList();

        return new TeamspeakAccountsDataset
        {
            Commanders = commanders,
            Recruiters = recruiters,
            Members = members,
            Guests = guests,
        };
    }

    [HttpGet("{accountId}/onlineUserDetails")]
    [Permissions(Permissions.Recruiter)]
    public OnlineState GetOnlineUserDetails([FromRoute] string accountId)
    {
        return _teamspeakService.GetOnlineUserDetails(accountId);
    }
}
