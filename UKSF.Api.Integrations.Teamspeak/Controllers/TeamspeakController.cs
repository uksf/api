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
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.Controllers {
    [Route("[controller]")]
    public class TeamspeakController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IDisplayNameService _displayNameService;
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
            IDisplayNameService displayNameService
        ) {
            _accountContext = accountContext;
            _teamspeakService = teamspeakService;
            _ranksService = ranksService;
            _unitsService = unitsService;
            _recruitmentService = recruitmentService;
            _displayNameService = displayNameService;
        }

        [HttpGet("online"), Authorize, Permissions(Permissions.CONFIRMED, Permissions.MEMBER, Permissions.DISCHARGED)]
        public IEnumerable<object> GetOnlineClients() => _teamspeakService.GetFormattedClients();

        [HttpGet("shutdown"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> Shutdown() {
            await _teamspeakService.Shutdown();
            await Task.Delay(TimeSpan.FromSeconds(3));
            return Ok();
        }

        // TODO: Frontend needs reference updating
        [HttpGet("onlineAccounts")]
        public IActionResult GetOnlineAccounts() {
            IEnumerable<TeamspeakClient> teamnspeakClients = _teamspeakService.GetOnlineTeamspeakClients();
            IEnumerable<Account> allAccounts = _accountContext.Get();
            var clients = teamnspeakClients.Where(x => x != null)
                                           .Select(
                                               x => new {
                                                   account = allAccounts.FirstOrDefault(y => y.TeamspeakIdentities != null && y.TeamspeakIdentities.Any(z => z.Equals(x.ClientDbId))), client = x
                                               }
                                           )
                                           .ToList();
            var clientAccounts = clients.Where(x => x.account != null && x.account.MembershipState == MembershipState.MEMBER)
                                        .OrderBy(x => x.account.Rank, new RankComparer(_ranksService))
                                        .ThenBy(x => x.account.Lastname)
                                        .ThenBy(x => x.account.Firstname);
            List<string> commandAccounts = _unitsService.GetAuxilliaryRoot().Members;

            List<object> commanders = new();
            List<object> recruiters = new();
            List<object> members = new();
            List<object> guests = new();
            foreach (var onlineClient in clientAccounts) {
                if (commandAccounts.Contains(onlineClient.account.Id)) {
                    commanders.Add(new { displayName = _displayNameService.GetDisplayName(onlineClient.account) });
                } else if (_recruitmentService.IsRecruiter(onlineClient.account)) {
                    recruiters.Add(new { displayName = _displayNameService.GetDisplayName(onlineClient.account) });
                } else {
                    members.Add(new { displayName = _displayNameService.GetDisplayName(onlineClient.account) });
                }
            }

            foreach (var client in clients.Where(x => x.account == null || x.account.MembershipState != MembershipState.MEMBER)) {
                guests.Add(new { displayName = client.client.ClientName });
            }

            return Ok(new { commanders, recruiters, members, guests });
        }

        // TODO: Use in frontend. Check return type. Check permissions
        [HttpGet("onlineUserDetails"), Authorize]
        public (bool tsOnline, string tsNickname) GetOnlineUserDetails(Account account) => _teamspeakService.GetOnlineUserDetails(account);
    }
}
