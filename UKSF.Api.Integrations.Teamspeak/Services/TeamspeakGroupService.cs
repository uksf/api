using MongoDB.Bson;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;

namespace UKSF.Api.Integrations.Teamspeak.Services;

public interface ITeamspeakGroupService
{
    Task UpdateAccountGroups(DomainAccount account, ICollection<int> serverGroups, int clientDbId);
}

public class TeamspeakGroupService(
    IRanksContext ranksContext,
    IUnitsContext unitsContext,
    IUnitsService unitsService,
    ITeamspeakManagerService teamspeakManagerService,
    IVariablesService variablesService,
    ITrainingsContext trainingsContext,
    IUpdateAccountTrainingCommandHandler updateAccountTrainingCommandHandler
) : ITeamspeakGroupService
{
    public async Task UpdateAccountGroups(DomainAccount account, ICollection<int> serverGroups, int clientDbId)
    {
        HashSet<int> memberGroups = [];

        if (account is null)
        {
            memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsInt());
        }
        else
        {
            switch (account.MembershipState)
            {
                case MembershipState.Unconfirmed:
                    memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsInt());
                    break;
                case MembershipState.Discharged:
                    memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_DISCHARGED").AsInt());
                    break;
                case MembershipState.Confirmed:
                    ResolveRankGroup(account, memberGroups);
                    break;
                case MembershipState.Member:
                    ResolveRankGroup(account, memberGroups);
                    ResolveUnitGroup(account, memberGroups);
                    ResolveParentUnitGroup(account, memberGroups);
                    ResolveAuxiliaryUnitGroups(account, memberGroups);
                    memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_ROOT").AsInt());
                    break;
            }
        }

        var groupsBlacklist = variablesService.GetVariable("TEAMSPEAK_GID_BLACKLIST").AsIntArray().ToList();
        foreach (var serverGroup in serverGroups)
        {
            if (!memberGroups.Contains(serverGroup) && !groupsBlacklist.Contains(serverGroup))
            {
                await RemoveServerGroup(clientDbId, serverGroup);
            }
        }

        foreach (var serverGroup in memberGroups.Where(serverGroup => !serverGroups.Contains(serverGroup)))
        {
            await AddServerGroup(clientDbId, serverGroup);
        }

        // Temporary migration of TS training groups to account training data
        if (account is not null)
        {
            var missingAccountTrainings = serverGroups.Select(x => trainingsContext.GetSingle(y => y.TeamspeakGroup == x.ToString()))
                                                      .Where(x => x is not null)
                                                      .Select(x => x.Id)
                                                      .Where(x => !account.Trainings.Contains(x))
                                                      .ToList();
            var allAccountTrainings = account.Trainings.Concat(missingAccountTrainings).Distinct().ToList();
            await updateAccountTrainingCommandHandler.ExecuteAsync(new UpdateAccountTrainingCommand(account.Id, allAccountTrainings));
        }
    }

    private void ResolveRankGroup(DomainAccount account, HashSet<int> memberGroups)
    {
        if (string.IsNullOrEmpty(account.Rank))
        {
            return;
        }

        memberGroups.Add(ranksContext.GetSingle(account.Rank).TeamspeakGroup.ToInt());
    }

    private void ResolveUnitGroup(DomainAccount account, HashSet<int> memberGroups)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        var elcom = unitsService.GetAuxiliaryRoot();

        if (accountUnit.Parent == ObjectId.Empty.ToString())
        {
            memberGroups.Add(accountUnit.TeamspeakGroup.ToInt());
        }

        var group = elcom.Members.Contains(account.Id)
            ? variablesService.GetVariable("TEAMSPEAK_GID_ELCOM").AsInt()
            : accountUnit.TeamspeakGroup.ToInt();
        if (group == 0)
        {
            ResolveParentUnitGroup(account, memberGroups);
        }
        else
        {
            memberGroups.Add(group);
        }
    }

    private void ResolveParentUnitGroup(DomainAccount account, HashSet<int> memberGroups)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        var parentUnit = unitsService.GetParents(accountUnit)
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

    private void ResolveAuxiliaryUnitGroups(MongoObject account, HashSet<int> memberGroups)
    {
        var accountUnits = unitsContext.Get(x => x.Parent != ObjectId.Empty.ToString() && x.Branch == UnitBranch.Auxiliary && x.Members.Contains(account.Id))
                                       .Where(x => !string.IsNullOrEmpty(x.TeamspeakGroup));
        foreach (var unit in accountUnits)
        {
            memberGroups.Add(unit.TeamspeakGroup.ToInt());
        }
    }

    private async Task AddServerGroup(int clientDbId, int serverGroup)
    {
        await teamspeakManagerService.SendGroupProcedure(
            TeamspeakProcedureType.Assign,
            new TeamspeakGroupProcedure { ClientDbId = clientDbId, ServerGroup = serverGroup }
        );
    }

    private async Task RemoveServerGroup(int clientDbId, int serverGroup)
    {
        await teamspeakManagerService.SendGroupProcedure(
            TeamspeakProcedureType.Unassign,
            new TeamspeakGroupProcedure { ClientDbId = clientDbId, ServerGroup = serverGroup }
        );
    }
}
