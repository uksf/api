using System.Text;
using AvsAnLib;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IAssignmentService
{
    Task AssignUnitRole(string roleId, string unitId, string role);
    Task UnassignAllUnits(string id);
    Task UnassignAllUnitRoles(string id);

    Task<DomainNotification> UpdateUnitRankAndRole(
        string id,
        string unitString = "",
        string role = "",
        string rankString = "",
        string notes = "",
        string message = "",
        string reason = ""
    );

    Task<string> UnassignUnitRole(string roleId, string unitId);
    Task UnassignUnit(string id, string unitId);
    void UpdateGroupsAndRoles(string id);
}

public class AssignmentService(
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    IServiceRecordService serviceRecordService,
    IRanksService ranksService,
    IUnitsService unitsService,
    IDisplayNameService displayNameService,
    IEventBus eventBus
) : IAssignmentService
{
    public const string RemoveFlag = "REMOVE";

    public async Task<DomainNotification> UpdateUnitRankAndRole(
        string id,
        string unitString = "",
        string role = "",
        string rankString = "",
        string notes = "",
        string message = "",
        string reason = ""
    )
    {
        StringBuilder notificationBuilder = new();

        var (unitUpdate, unitPositive) = await UpdateUnit(id, unitString, notificationBuilder);
        var (roleUpdate, rolePositive) = await UpdateRole(id, role, unitUpdate, notificationBuilder);
        var (rankUpdate, rankPositive) = await UpdateRank(id, rankString, unitUpdate, roleUpdate, notificationBuilder);
        bool positive;
        if (rankPositive)
        {
            positive = true;
        }
        else
        {
            positive = unitPositive || rolePositive;
        }

        if (!unitUpdate && !roleUpdate && !rankUpdate)
        {
            return null;
        }

        if (string.IsNullOrEmpty(message))
        {
            message = notificationBuilder.ToString();
            if (!string.IsNullOrEmpty(reason))
            {
                message = $"{message} because {reason}";
            }

            if (rankUpdate)
            {
                message = $"{message}. Please change your TeamSpeak and Arma name to {displayNameService.GetDisplayName(id)}";
            }
        }

        serviceRecordService.AddServiceRecord(id, message, notes);
        UpdateGroupsAndRoles(id);
        return message != RemoveFlag
            ? new DomainNotification
            {
                Owner = id,
                Message = message,
                Icon = positive ? NotificationIcons.Promotion : NotificationIcons.Demotion
            }
            : null;
    }

    public async Task AssignUnitRole(string roleId, string unitId, string role)
    {
        await unitsService.SetMemberRole(roleId, unitId, role);
        UpdateGroupsAndRoles(roleId);
    }

    public async Task UnassignAllUnits(string id)
    {
        foreach (var unit in unitsContext.Get())
        {
            await unitsService.RemoveMember(id, unit);
        }

        UpdateGroupsAndRoles(id);
    }

    public async Task UnassignAllUnitRoles(string id)
    {
        foreach (var unit in unitsContext.Get())
        {
            await unitsService.SetMemberRole(id, unit);
        }

        UpdateGroupsAndRoles(id);
    }

    public async Task<string> UnassignUnitRole(string roleId, string unitId)
    {
        var unit = unitsContext.GetSingle(unitId);
        var role = unit.Roles.FirstOrDefault(x => x.Value == roleId).Key;
        if (unitsService.RolesHasMember(unit, roleId))
        {
            await unitsService.SetMemberRole(roleId, unitId);
            UpdateGroupsAndRoles(roleId);
        }

        return role;
    }

    public async Task UnassignUnit(string id, string unitId)
    {
        var unit = unitsContext.GetSingle(unitId);
        await unitsService.RemoveMember(id, unit);
        UpdateGroupsAndRoles(id);
    }

    public void UpdateGroupsAndRoles(string id)
    {
        var account = accountContext.GetSingle(id);
        eventBus.Send(new ContextEventData<DomainAccount>(id, account), nameof(AssignmentService));
    }

    private async Task<Tuple<bool, bool>> UpdateUnit(string id, string unitString, StringBuilder notificationMessage)
    {
        var unitUpdate = false;
        var positive = true;
        var unit = unitsContext.GetSingle(x => x.Name == unitString);
        if (unit is not null)
        {
            if (unit.Branch == UnitBranch.Combat)
            {
                await unitsService.RemoveMember(id, accountContext.GetSingle(id).UnitAssignment);
                await accountContext.Update(id, x => x.UnitAssignment, unit.Name);
            }

            await unitsService.AddMember(id, unit.Id);
            notificationMessage.Append($"You have been transferred to {unitsService.GetChainString(unit)}");
            unitUpdate = true;
        }
        else if (unitString == RemoveFlag)
        {
            var currentUnit = accountContext.GetSingle(id).UnitAssignment;
            if (string.IsNullOrEmpty(currentUnit))
            {
                return new Tuple<bool, bool>(false, false);
            }

            unit = unitsContext.GetSingle(x => x.Name == currentUnit);
            await unitsService.RemoveMember(id, currentUnit);
            await accountContext.Update(id, x => x.UnitAssignment, null);
            notificationMessage.Append($"You have been removed from {unitsService.GetChainString(unit)}");
            unitUpdate = true;
            positive = false;
        }

        return new Tuple<bool, bool>(unitUpdate, positive);
    }

    private async Task<Tuple<bool, bool>> UpdateRole(string id, string role, bool unitUpdate, StringBuilder notificationMessage)
    {
        var roleUpdate = false;
        var positive = true;
        if (!string.IsNullOrEmpty(role) && role != RemoveFlag)
        {
            await accountContext.Update(id, x => x.RoleAssignment, role);
            notificationMessage.Append(
                $"{(unitUpdate ? $" as {AvsAn.Query(role).Article} {role}" : $"You have been assigned as {AvsAn.Query(role).Article} {role}")}"
            );
            roleUpdate = true;
        }
        else if (role == RemoveFlag)
        {
            var currentRole = accountContext.GetSingle(id).RoleAssignment;
            await accountContext.Update(id, x => x.RoleAssignment, null);
            notificationMessage.Append(
                string.IsNullOrEmpty(currentRole)
                    ? $"{(unitUpdate ? " and unassigned from your role" : "You have been unassigned from your role")}"
                    : $"{(unitUpdate ? $" and unassigned as {AvsAn.Query(currentRole).Article} {currentRole}" : $"You have been unassigned as {AvsAn.Query(currentRole).Article} {currentRole}")}"
            );

            roleUpdate = true;
            positive = false;
        }

        return new Tuple<bool, bool>(roleUpdate, positive);
    }

    private async Task<Tuple<bool, bool>> UpdateRank(string id, string rank, bool unitUpdate, bool roleUpdate, StringBuilder notificationMessage)
    {
        var rankUpdate = false;
        var positive = true;
        var currentRank = accountContext.GetSingle(id).Rank;
        if (!string.IsNullOrEmpty(rank) && rank != RemoveFlag)
        {
            if (currentRank == rank)
            {
                return new Tuple<bool, bool>(false, true);
            }

            await accountContext.Update(id, x => x.Rank, rank);
            var promotion = string.IsNullOrEmpty(currentRank) || ranksService.IsSuperior(rank, currentRank);
            notificationMessage.Append(
                $"{(unitUpdate || roleUpdate ? $" and {(promotion ? "promoted" : "demoted")} to {rank}" : $"You have been {(promotion ? "promoted" : "demoted")} to {rank}")}"
            );
            rankUpdate = true;
        }
        else if (rank == RemoveFlag)
        {
            await accountContext.Update(id, x => x.Rank, null);
            notificationMessage.Append($"{(unitUpdate || roleUpdate ? $" and demoted from {currentRank}" : $"You have been demoted from {currentRank}")}");
            rankUpdate = true;
            positive = false;
        }

        return new Tuple<bool, bool>(rankUpdate, positive);
    }
}
