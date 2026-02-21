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
    private const string TeamspeakGidUnverified = "TEAMSPEAK_GID_UNVERIFIED";
    private const string TeamspeakGidDischarged = "TEAMSPEAK_GID_DISCHARGED";
    private const string TeamspeakGidRoot = "TEAMSPEAK_GID_ROOT";
    private const string TeamspeakGidElcom = "TEAMSPEAK_GID_ELCOM";
    private const string TeamspeakGidBlacklist = "TEAMSPEAK_GID_BLACKLIST";

    public async Task UpdateAccountGroups(DomainAccount account, ICollection<int> assignedGroupIds, int clientDbId)
    {
        var groupIdsToAssign = ResolveMembershipGroups(account);
        await ApplyGroupChanges(groupIdsToAssign, assignedGroupIds, clientDbId);
    }

    private HashSet<int> ResolveMembershipGroups(DomainAccount account)
    {
        HashSet<int> groupIdsToAssign = [];

        if (account is null)
        {
            AddGroup(groupIdsToAssign, TeamspeakGidUnverified);
            return groupIdsToAssign;
        }

        switch (account.MembershipState)
        {
            case MembershipState.Unconfirmed: AddGroup(groupIdsToAssign, TeamspeakGidUnverified); break;
            case MembershipState.Discharged:  AddGroup(groupIdsToAssign, TeamspeakGidDischarged); break;
            case MembershipState.Confirmed:   ResolveRankGroup(account, groupIdsToAssign); break;
            case MembershipState.Member:      ResolveMemberGroups(account, groupIdsToAssign); break;
        }

        return groupIdsToAssign;
    }

    private void ResolveMemberGroups(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        ResolveRankGroup(account, groupIdsToAssign);
        ResolveUnitGroup(account, groupIdsToAssign);
        ResolveParentUnitGroup(account, groupIdsToAssign);
        ResolveNonCombatUnitGroups(account, groupIdsToAssign);
        ResolveTrainingGroups(account, groupIdsToAssign);
        AddGroup(groupIdsToAssign, TeamspeakGidRoot);
    }

    private async Task ApplyGroupChanges(HashSet<int> groupIdsToAssign, ICollection<int> assignedGroupIds, int clientDbId)
    {
        var groupIdBlacklist = variablesService.GetVariable(TeamspeakGidBlacklist).AsIntArray().ToList();

        await RemoveUnwantedGroups(assignedGroupIds, groupIdsToAssign, groupIdBlacklist, clientDbId);
        await AddMissingGroups(groupIdsToAssign, assignedGroupIds, clientDbId);
    }

    private async Task RemoveUnwantedGroups(ICollection<int> assignedGroupIds, HashSet<int> groupIdsToAssign, List<int> groupIdBlacklist, int clientDbId)
    {
        foreach (var assignedGroupId in assignedGroupIds)
        {
            if (!groupIdsToAssign.Contains(assignedGroupId) && !groupIdBlacklist.Contains(assignedGroupId))
            {
                await RemoveServerGroup(clientDbId, assignedGroupId);
            }
        }
    }

    private async Task AddMissingGroups(HashSet<int> groupIdsToAssign, ICollection<int> assignedGroupIds, int clientDbId)
    {
        foreach (var serverGroup in groupIdsToAssign.Where(serverGroup => !assignedGroupIds.Contains(serverGroup)))
        {
            await AddServerGroup(clientDbId, serverGroup);
        }
    }

    private void AddGroup(HashSet<int> groupIdsToAssign, string variableKey)
    {
        groupIdsToAssign.Add(variablesService.GetVariable(variableKey).AsInt());
    }

    private void ResolveRankGroup(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        if (string.IsNullOrEmpty(account.Rank))
        {
            return;
        }

        var rank = ranksContext.GetSingle(account.Rank);
        if (rank == null)
        {
            return;
        }

        groupIdsToAssign.Add(rank.TeamspeakGroup.ToInt());
    }

    private void ResolveUnitGroup(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        if (accountUnit == null)
        {
            return;
        }

        var elcom = unitsService.GetAuxiliaryRoot();

        if (accountUnit.Parent == ObjectId.Empty.ToString())
        {
            var groupId = GetUnitGroupId(account, accountUnit, elcom);
            groupIdsToAssign.Add(groupId);
            return;
        }

        var unitGroupId = GetUnitGroupId(account, accountUnit, elcom);
        if (unitGroupId == 0)
        {
            ResolveParentUnitGroup(account, groupIdsToAssign);
        }
        else
        {
            groupIdsToAssign.Add(unitGroupId);
        }
    }

    private int GetUnitGroupId(DomainAccount account, DomainUnit accountUnit, DomainUnit elcom)
    {
        return elcom.Members.Contains(account.Id) ? variablesService.GetVariable(TeamspeakGidElcom).AsInt() : accountUnit.TeamspeakGroup.ToInt();
    }

    private void ResolveParentUnitGroup(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        if (accountUnit == null)
        {
            return;
        }

        var parentUnit = FindValidParentUnit(accountUnit, groupIdsToAssign);

        if (parentUnit is not null && parentUnit.Parent != ObjectId.Empty.ToString())
        {
            groupIdsToAssign.Add(parentUnit.TeamspeakGroup.ToInt());
        }
        else
        {
            groupIdsToAssign.Add(accountUnit.TeamspeakGroup.ToInt());
        }
    }

    private DomainUnit FindValidParentUnit(DomainUnit accountUnit, HashSet<int> groupIdsToAssign)
    {
        return unitsService.GetParents(accountUnit)
                           .Skip(1)
                           .FirstOrDefault(x => !string.IsNullOrEmpty(x.TeamspeakGroup) && !groupIdsToAssign.Contains(x.TeamspeakGroup.ToInt()));
    }

    private void ResolveNonCombatUnitGroups(MongoObject account, HashSet<int> groupIdsToAssign)
    {
        var accountUnits = GetNonCombatUnits(account);
        foreach (var unit in accountUnits)
        {
            groupIdsToAssign.Add(unit.TeamspeakGroup.ToInt());
        }
    }

    private IEnumerable<DomainUnit> GetNonCombatUnits(MongoObject account)
    {
        return unitsContext.Get(x => x.Parent != ObjectId.Empty.ToString() && x.Branch is UnitBranch.Auxiliary && x.Members.Contains(account.Id))
                           .Where(x => !string.IsNullOrEmpty(x.TeamspeakGroup));
    }

    private void ResolveTrainingGroups(DomainAccount account, HashSet<int> groupIdsToAssign)
    {
        var trainingGroupIds = GetTrainingGroupIds(account);
        foreach (var groupId in trainingGroupIds)
        {
            groupIdsToAssign.Add(groupId);
        }
    }

    private IEnumerable<int> GetTrainingGroupIds(DomainAccount account)
    {
        return trainingsContext.Get().Where(x => account.Trainings.Contains(x.Id)).Select(training => training.TeamspeakGroup.ToInt());
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
