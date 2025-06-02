using MongoDB.Bson;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;

namespace UKSF.Api.Integrations.Teamspeak.Services;

public interface ITeamspeakGroupService
{
    Task UpdateAccountGroups(DomainAccount account, ICollection<int> assignedGroupIds, int clientDbId);
}

public class TeamspeakGroupService(
    IRanksContext ranksContext,
    IUnitsContext unitsContext,
    IUnitsService unitsService,
    ITeamspeakManagerService teamspeakManagerService,
    IVariablesService variablesService,
    ITrainingsContext trainingsContext
) : ITeamspeakGroupService
{
    public async Task UpdateAccountGroups(DomainAccount account, ICollection<int> assignedGroupIds, int clientDbId)
    {
        HashSet<int> groupIdsToAssign = [];

        if (account is null)
        {
            groupIdsToAssign.Add(variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsInt());
        }
        else
        {
            switch (account.MembershipState)
            {
                case MembershipState.Unconfirmed: groupIdsToAssign.Add(variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsInt()); break;
                case MembershipState.Discharged:  groupIdsToAssign.Add(variablesService.GetVariable("TEAMSPEAK_GID_DISCHARGED").AsInt()); break;
                case MembershipState.Confirmed:   ResolveRankGroup(account, groupIdsToAssign); break;
                case MembershipState.Member:
                    ResolveRankGroup(account, groupIdsToAssign);
                    ResolveUnitGroup(account, groupIdsToAssign);
                    ResolveParentUnitGroup(account, groupIdsToAssign);
                    ResolveNonCombatUnitGroups(account, groupIdsToAssign);
                    ResolveTrainingGroups(account, groupIdsToAssign);
                    groupIdsToAssign.Add(variablesService.GetVariable("TEAMSPEAK_GID_ROOT").AsInt());
                    break;
            }
        }

        var groupIdBlacklist = variablesService.GetVariable("TEAMSPEAK_GID_BLACKLIST").AsIntArray().ToList();
        foreach (var assignedGroupId in assignedGroupIds)
        {
            if (!groupIdsToAssign.Contains(assignedGroupId) && !groupIdBlacklist.Contains(assignedGroupId))
            {
                await RemoveServerGroup(clientDbId, assignedGroupId);
            }
        }

        foreach (var serverGroup in groupIdsToAssign.Where(serverGroup => !assignedGroupIds.Contains(serverGroup)))
        {
            await AddServerGroup(clientDbId, serverGroup);
        }
    }

    private void ResolveRankGroup(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        if (string.IsNullOrEmpty(account.Rank))
        {
            return;
        }

        groupIdsToAssign.Add(ranksContext.GetSingle(account.Rank).TeamspeakGroup.ToInt());
    }

    private void ResolveUnitGroup(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        var elcom = unitsService.GetAuxiliaryRoot();

        if (accountUnit.Parent == ObjectId.Empty.ToString())
        {
            groupIdsToAssign.Add(accountUnit.TeamspeakGroup.ToInt());
        }

        var group = elcom.Members.Contains(account.Id) ? variablesService.GetVariable("TEAMSPEAK_GID_ELCOM").AsInt() : accountUnit.TeamspeakGroup.ToInt();
        if (group == 0)
        {
            ResolveParentUnitGroup(account, groupIdsToAssign);
        }
        else
        {
            groupIdsToAssign.Add(group);
        }
    }

    private void ResolveParentUnitGroup(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        var parentUnit = unitsService.GetParents(accountUnit)
                                     .Skip(1)
                                     .FirstOrDefault(x => !string.IsNullOrEmpty(x.TeamspeakGroup) && !groupIdsToAssign.Contains(x.TeamspeakGroup.ToInt()));
        if (parentUnit is not null && parentUnit.Parent != ObjectId.Empty.ToString())
        {
            groupIdsToAssign.Add(parentUnit.TeamspeakGroup.ToInt());
        }
        else
        {
            groupIdsToAssign.Add(accountUnit.TeamspeakGroup.ToInt());
        }
    }

    private void ResolveNonCombatUnitGroups(MongoObject account, HashSet<int> groupIdsToAssign)
    {
        var accountUnits = unitsContext.Get(x => x.Parent != ObjectId.Empty.ToString() && x.Branch is UnitBranch.Auxiliary && x.Members.Contains(account.Id))
                                       .Where(x => !string.IsNullOrEmpty(x.TeamspeakGroup));
        foreach (var unit in accountUnits)
        {
            groupIdsToAssign.Add(unit.TeamspeakGroup.ToInt());
        }
    }

    private void ResolveTrainingGroups(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        foreach (var accountTrainingGroupId in account.Trainings.Select(x => trainingsContext.GetSingle(y => y.TeamspeakGroup == x.ToString()))
                                                      .Where(x => x is not null)
                                                      .Select(x => x.TeamspeakGroup.ToInt()))
        {
            groupIdsToAssign.Add(accountTrainingGroupId);
        }
    }

    private Task AddServerGroup(int clientDbId, int serverGroup)
    {
        return teamspeakManagerService.SendGroupProcedure(
            TeamspeakProcedureType.Assign,
            new TeamspeakGroupProcedure { ClientDbId = clientDbId, ServerGroup = serverGroup }
        );
    }

    private Task RemoveServerGroup(int clientDbId, int serverGroup)
    {
        return teamspeakManagerService.SendGroupProcedure(
            TeamspeakProcedureType.Unassign,
            new TeamspeakGroupProcedure { ClientDbId = clientDbId, ServerGroup = serverGroup }
        );
    }
}
