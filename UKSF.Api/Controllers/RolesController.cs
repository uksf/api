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
    IRolesContext rolesContext,
    IAccountContext accountContext,
    IAssignmentService assignmentService,
    INotificationsService notificationsService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public RolesDataset GetRoles([FromQuery] string id = "")
    {
        if (!string.IsNullOrEmpty(id))
        {
            var account = accountContext.GetSingle(id);
            return new RolesDataset { Roles = rolesContext.Get(x => x.Name != account.RoleAssignment) };
        }

        return new RolesDataset { Roles = rolesContext.Get() };
    }

    [HttpPost("{check}")]
    [Authorize]
    public DomainRole CheckRole([FromRoute] string check, [FromBody] DomainRole role = null)
    {
        if (string.IsNullOrEmpty(check))
        {
            return null;
        }

        if (role is not null)
        {
            var safeRole = role;
            return rolesContext.GetSingle(x => x.Id != safeRole.Id && x.Name == check);
        }

        return rolesContext.GetSingle(x => x.Name == check);
    }

    [HttpPut]
    [Authorize]
    public async Task<RolesDataset> AddRole([FromBody] DomainRole role)
    {
        await rolesContext.Add(role);
        logger.LogAudit($"Role added '{role.Name}'");
        return new RolesDataset { Roles = rolesContext.Get() };
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

        return new RolesDataset { Roles = rolesContext.Get() };
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

        return new RolesDataset { Roles = rolesContext.Get() };
    }
}
