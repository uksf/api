using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.Controllers
{
    [Route("teamspeak")]
    public class TeamspeakController : ControllerBase
    {
        private readonly IAccountContext _accountContext;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IDisplayNameService _displayNameService;
        private readonly INotificationsService _notificationsService;
        private readonly ILogger _logger;
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
            ILogger logger
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

        [HttpGet("{teamspeakId}"), Authorize]
        public async Task RequestTeamspeakCode([FromRoute] string teamspeakId)
        {
            string code = await _confirmationCodeService.CreateConfirmationCode(teamspeakId);
            _notificationsService.SendTeamspeakNotification(
                new HashSet<int> { teamspeakId.ToInt() },
                $"This Teamspeak ID was selected for connection to the website. Copy this code to your clipboard and return to the UKSF website application page to enter the code:\n{code}\nIf this request was not made by you, it is safe to ignore. Do not pass this code on to anyone else."
            );
        }

        [HttpGet("online"), Permissions(Permissions.CONFIRMED, Permissions.MEMBER, Permissions.DISCHARGED)]
        public IEnumerable<object> GetOnlineClients()
        {
            return _teamspeakService.GetFormattedClients();
        }

        [HttpGet("reload"), Permissions(Permissions.ADMIN)]
        public async Task Reload()
        {
            _logger.LogInfo("Teampseak reload via API");
            await _teamspeakService.Reload();
        }

        [HttpGet("shutdown"), Permissions(Permissions.ADMIN)]
        public async Task Shutdown()
        {
            _logger.LogInfo("Teampseak shutdown via API");
            await _teamspeakService.Shutdown();
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        [HttpGet("onlineAccounts")]
        public TeamspeakAccountsDataset GetOnlineAccounts()
        {
            IEnumerable<TeamspeakClient> teamnspeakClients = _teamspeakService.GetOnlineTeamspeakClients();
            IEnumerable<DomainAccount> allAccounts = _accountContext.Get();
            var clients = teamnspeakClients.Where(x => x != null)
                                           .Select(
                                               x => new
                                               {
                                                   account = allAccounts.FirstOrDefault(y => y.TeamspeakIdentities != null && y.TeamspeakIdentities.Any(z => z.Equals(x.ClientDbId))), client = x
                                               }
                                           )
                                           .ToList();
            var clientAccounts = clients.Where(x => x.account is { MembershipState: MembershipState.MEMBER })
                                        .OrderBy(x => x.account.Rank, new RankComparer(_ranksService))
                                        .ThenBy(x => x.account.Lastname)
                                        .ThenBy(x => x.account.Firstname);
            List<string> commandAccounts = _unitsService.GetAuxilliaryRoot().Members;

            List<TeamspeakAccountDataset> commanders = new();
            List<TeamspeakAccountDataset> recruiters = new();
            List<TeamspeakAccountDataset> members = new();
            foreach (var onlineClient in clientAccounts)
            {
                if (commandAccounts.Contains(onlineClient.account.Id))
                {
                    commanders.Add(new() { DisplayName = _displayNameService.GetDisplayName(onlineClient.account) });
                }
                else if (_recruitmentService.IsRecruiter(onlineClient.account))
                {
                    recruiters.Add(new() { DisplayName = _displayNameService.GetDisplayName(onlineClient.account) });
                }
                else
                {
                    members.Add(new() { DisplayName = _displayNameService.GetDisplayName(onlineClient.account) });
                }
            }

            List<TeamspeakAccountDataset> guests = clients.Where(x => x.account is not { MembershipState: MembershipState.MEMBER })
                                                          .Select(client => (TeamspeakAccountDataset) new() { DisplayName = client.client.ClientName })
                                                          .ToList();

            return new() { Commanders = commanders, Recruiters = recruiters, Members = members, Guests = guests };
        }

        [HttpGet("{accountId}/onlineUserDetails"), Permissions(Permissions.RECRUITER)]
        public OnlineState GetOnlineUserDetails([FromRoute] string accountId)
        {
            return _teamspeakService.GetOnlineUserDetails(accountId);
        }
    }
}
