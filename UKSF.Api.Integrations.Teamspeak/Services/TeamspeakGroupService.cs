using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Teamspeak.Models;

namespace UKSF.Api.Teamspeak.Services
{
    public interface ITeamspeakGroupService
    {
        Task UpdateAccountGroups(DomainAccount domainAccount, ICollection<int> serverGroups, int clientDbId);
    }

    public class TeamspeakGroupService : ITeamspeakGroupService
    {
        private readonly IRanksContext _ranksContext;
        private readonly ITeamspeakManagerService _teamspeakManagerService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;
        private readonly IVariablesService _variablesService;

        public TeamspeakGroupService(
            IRanksContext ranksContext,
            IUnitsContext unitsContext,
            IUnitsService unitsService,
            ITeamspeakManagerService teamspeakManagerService,
            IVariablesService variablesService
        )
        {
            _ranksContext = ranksContext;
            _unitsContext = unitsContext;
            _unitsService = unitsService;
            _teamspeakManagerService = teamspeakManagerService;
            _variablesService = variablesService;
        }

        public async Task UpdateAccountGroups(DomainAccount domainAccount, ICollection<int> serverGroups, int clientDbId)
        {
            HashSet<int> memberGroups = new();

            if (domainAccount == null)
            {
                memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsInt());
            }
            else
            {
                switch (domainAccount.MembershipState)
                {
                    case MembershipState.UNCONFIRMED:
                        memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsInt());
                        break;
                    case MembershipState.DISCHARGED:
                        memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_DISCHARGED").AsInt());
                        break;
                    case MembershipState.CONFIRMED:
                        ResolveRankGroup(domainAccount, memberGroups);
                        break;
                    case MembershipState.MEMBER:
                        ResolveRankGroup(domainAccount, memberGroups);
                        ResolveUnitGroup(domainAccount, memberGroups);
                        ResolveParentUnitGroup(domainAccount, memberGroups);
                        ResolveAuxiliaryUnitGroups(domainAccount, memberGroups);
                        memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_ROOT").AsInt());
                        break;
                }
            }

            List<int> groupsBlacklist = _variablesService.GetVariable("TEAMSPEAK_GID_BLACKLIST").AsIntArray().ToList();
            foreach (int serverGroup in serverGroups)
            {
                if (!memberGroups.Contains(serverGroup) && !groupsBlacklist.Contains(serverGroup))
                {
                    await RemoveServerGroup(clientDbId, serverGroup);
                }
            }

            foreach (int serverGroup in memberGroups.Where(serverGroup => !serverGroups.Contains(serverGroup)))
            {
                await AddServerGroup(clientDbId, serverGroup);
            }
        }

        private void ResolveRankGroup(DomainAccount domainAccount, ISet<int> memberGroups)
        {
            memberGroups.Add(_ranksContext.GetSingle(domainAccount.Rank).TeamspeakGroup.ToInt());
        }

        private void ResolveUnitGroup(DomainAccount domainAccount, ISet<int> memberGroups)
        {
            var accountUnit = _unitsContext.GetSingle(x => x.Name == domainAccount.UnitAssignment);
            var elcom = _unitsService.GetAuxilliaryRoot();

            if (accountUnit.Parent == ObjectId.Empty.ToString())
            {
                memberGroups.Add(accountUnit.TeamspeakGroup.ToInt());
            }

            int group = elcom.Members.Contains(domainAccount.Id)
                ? _variablesService.GetVariable("TEAMSPEAK_GID_ELCOM").AsInt()
                : accountUnit.TeamspeakGroup.ToInt();
            if (group == 0)
            {
                ResolveParentUnitGroup(domainAccount, memberGroups);
            }
            else
            {
                memberGroups.Add(group);
            }
        }

        private void ResolveParentUnitGroup(DomainAccount domainAccount, ISet<int> memberGroups)
        {
            var accountUnit = _unitsContext.GetSingle(x => x.Name == domainAccount.UnitAssignment);
            var parentUnit = _unitsService.GetParents(accountUnit)
                                          .Skip(1)
                                          .FirstOrDefault(x => !string.IsNullOrEmpty(x.TeamspeakGroup) && !memberGroups.Contains(x.TeamspeakGroup.ToInt()));
            if (parentUnit != null && parentUnit.Parent != ObjectId.Empty.ToString())
            {
                memberGroups.Add(parentUnit.TeamspeakGroup.ToInt());
            }
            else
            {
                memberGroups.Add(accountUnit.TeamspeakGroup.ToInt());
            }
        }

        private void ResolveAuxiliaryUnitGroups(MongoObject account, ISet<int> memberGroups)
        {
            var accountUnits = _unitsContext
                               .Get(x => x.Parent != ObjectId.Empty.ToString() && x.Branch == UnitBranch.AUXILIARY && x.Members.Contains(account.Id))
                               .Where(x => !string.IsNullOrEmpty(x.TeamspeakGroup));
            foreach (var unit in accountUnits)
            {
                memberGroups.Add(unit.TeamspeakGroup.ToInt());
            }
        }

        private async Task AddServerGroup(int clientDbId, int serverGroup)
        {
            await _teamspeakManagerService.SendGroupProcedure(TeamspeakProcedureType.ASSIGN, new() { ClientDbId = clientDbId, ServerGroup = serverGroup });
        }

        private async Task RemoveServerGroup(int clientDbId, int serverGroup)
        {
            await _teamspeakManagerService.SendGroupProcedure(TeamspeakProcedureType.UNASSIGN, new() { ClientDbId = clientDbId, ServerGroup = serverGroup });
        }
    }
}
