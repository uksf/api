using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class RolesController(
    IUnitsContext unitsContext,
    IRolesContext rolesContext,
    IAccountContext accountContext,
    IAssignmentService assignmentService,
    IUnitsService unitsService,
    INotificationsService notificationsService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public RolesDataset GetRoles([FromQuery] string id = "", [FromQuery] string unitId = "")
    {
        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(unitId))
        {
            var unit = unitsContext.GetSingle(unitId);
            var unitRoles = rolesContext.Get(x => x.RoleType == RoleType.Unit).OrderBy(x => x.Order);
            var existingPairs = unit.Roles.Where(x => x.Value == id);
            var filteredRoles = unitRoles.Where(x => existingPairs.All(y => y.Key != x.Name));
            return new RolesDataset { UnitRoles = filteredRoles };
        }

        if (!string.IsNullOrEmpty(id))
        {
            var account = accountContext.GetSingle(id);
            return new RolesDataset
            {
                IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual && x.Name != account.RoleAssignment).OrderBy(x => x.Order)
            };
        }

        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual),
            UnitRoles = rolesContext.Get(x => x.RoleType == RoleType.Unit).OrderBy(x => x.Order)
        };
    }

    [HttpPost("{roleType}/{check}")]
    [Authorize]
    public DomainRole CheckRole([FromRoute] RoleType roleType, [FromRoute] string check, [FromBody] DomainRole role = null)
    {
        if (string.IsNullOrEmpty(check))
        {
            return null;
        }

        if (role is not null)
        {
            var safeRole = role;
            return rolesContext.GetSingle(x => x.Id != safeRole.Id && x.RoleType == roleType && x.Name == check);
        }

        return rolesContext.GetSingle(x => x.RoleType == roleType && x.Name == check);
    }

    [HttpPut]
    [Authorize]
    public async Task<RolesDataset> AddRole([FromBody] DomainRole role)
    {
        await rolesContext.Add(role);
        logger.LogAudit($"Role added '{role.Name}'");
        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual),
            UnitRoles = rolesContext.Get(x => x.RoleType == RoleType.Unit).OrderBy(x => x.Order)
        };
    }

    [HttpPatch]
    [Authorize]
    public async Task<RolesDataset> EditRole([FromBody] DomainRole role)
    {
        var oldRole = rolesContext.GetSingle(x => x.Id == role.Id);
        logger.LogAudit($"Role updated from '{oldRole.Name}' to '{role.Name}'");
        await rolesContext.Update(role.Id, x => x.Name, role.Name);
        foreach (var account in accountContext.Get(x => x.RoleAssignment == oldRole.Name))
        {
            await accountContext.Update(account.Id, x => x.RoleAssignment, role.Name);
        }

        await unitsService.RenameRole(oldRole.Name, role.Name);
        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual),
            UnitRoles = rolesContext.Get(x => x.RoleType == RoleType.Unit).OrderBy(x => x.Order)
        };
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<RolesDataset> DeleteRole([FromRoute] string id)
    {
        var role = rolesContext.GetSingle(x => x.Id == id);
        logger.LogAudit($"Role deleted '{role.Name}'");
        await rolesContext.Delete(id);
        foreach (var account in accountContext.Get(x => x.RoleAssignment == role.Name))
        {
            var notification = await assignmentService.UpdateUnitRankAndRole(
                account.Id,
                role: AssignmentService.RemoveFlag,
                reason: $"the '{role.Name}' role was deleted"
            );
            notificationsService.Add(notification);
        }

        await unitsService.DeleteRole(role.Name);
        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual),
            UnitRoles = rolesContext.Get(x => x.RoleType == RoleType.Unit).OrderBy(x => x.Order)
        };
    }

    [HttpPost("order")]
    [Authorize]
    public async Task<IOrderedEnumerable<DomainRole>> UpdateOrder([FromBody] List<DomainRole> newRoleOrder)
    {
        for (var index = 0; index < newRoleOrder.Count; index++)
        {
            var role = newRoleOrder[index];
            if (rolesContext.GetSingle(role.Name).Order != index)
            {
                await rolesContext.Update(role.Id, x => x.Order, index);

            }
        }

        return rolesContext.Get(x => x.RoleType == RoleType.Unit).OrderBy(x => x.Order);
    }
}
