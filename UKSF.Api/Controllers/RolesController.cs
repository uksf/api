using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class RolesController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAssignmentService _assignmentService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IRolesContext _rolesContext;
    private readonly IUnitsContext _unitsContext;
    private readonly IUnitsService _unitsService;

    public RolesController(
        IUnitsContext unitsContext,
        IRolesContext rolesContext,
        IAccountContext accountContext,
        IAssignmentService assignmentService,
        IUnitsService unitsService,
        INotificationsService notificationsService,
        IUksfLogger logger
    )
    {
        _unitsContext = unitsContext;
        _rolesContext = rolesContext;
        _accountContext = accountContext;
        _assignmentService = assignmentService;
        _unitsService = unitsService;
        _notificationsService = notificationsService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public RolesDataset GetRoles([FromQuery] string id = "", [FromQuery] string unitId = "")
    {
        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(unitId))
        {
            var unit = _unitsContext.GetSingle(unitId);
            var unitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order);
            var existingPairs = unit.Roles.Where(x => x.Value == id);
            var filteredRoles = unitRoles.Where(x => existingPairs.All(y => y.Key != x.Name));
            return new() { UnitRoles = filteredRoles };
        }

        if (!string.IsNullOrEmpty(id))
        {
            var domainAccount = _accountContext.GetSingle(id);
            return new()
            {
                IndividualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL && x.Name != domainAccount.RoleAssignment).OrderBy(x => x.Order)
            };
        }

        return new()
        {
            IndividualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL),
            UnitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order)
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

        if (role != null)
        {
            var safeRole = role;
            return _rolesContext.GetSingle(x => x.Id != safeRole.Id && x.RoleType == roleType && x.Name == check);
        }

        return _rolesContext.GetSingle(x => x.RoleType == roleType && x.Name == check);
    }

    [HttpPut]
    [Authorize]
    public async Task<RolesDataset> AddRole([FromBody] DomainRole role)
    {
        await _rolesContext.Add(role);
        _logger.LogAudit($"Role added '{role.Name}'");
        return new()
        {
            IndividualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL),
            UnitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order)
        };
    }

    [HttpPatch]
    [Authorize]
    public async Task<RolesDataset> EditRole([FromBody] DomainRole role)
    {
        var oldRole = _rolesContext.GetSingle(x => x.Id == role.Id);
        _logger.LogAudit($"Role updated from '{oldRole.Name}' to '{role.Name}'");
        await _rolesContext.Update(role.Id, x => x.Name, role.Name);
        foreach (var account in _accountContext.Get(x => x.RoleAssignment == oldRole.Name))
        {
            await _accountContext.Update(account.Id, x => x.RoleAssignment, role.Name);
        }

        await _unitsService.RenameRole(oldRole.Name, role.Name);
        return new()
        {
            IndividualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL),
            UnitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order)
        };
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<RolesDataset> DeleteRole([FromRoute] string id)
    {
        var role = _rolesContext.GetSingle(x => x.Id == id);
        _logger.LogAudit($"Role deleted '{role.Name}'");
        await _rolesContext.Delete(id);
        foreach (var account in _accountContext.Get(x => x.RoleAssignment == role.Name))
        {
            var notification = await _assignmentService.UpdateUnitRankAndRole(
                account.Id,
                role: AssignmentService.RemoveFlag,
                reason: $"the '{role.Name}' role was deleted"
            );
            _notificationsService.Add(notification);
        }

        await _unitsService.DeleteRole(role.Name);
        return new()
        {
            IndividualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL),
            UnitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order)
        };
    }

    [HttpPost("order")]
    [Authorize]
    public async Task<IOrderedEnumerable<DomainRole>> UpdateOrder([FromBody] List<DomainRole> newRoleOrder)
    {
        for (var index = 0; index < newRoleOrder.Count; index++)
        {
            var role = newRoleOrder[index];
            if (_rolesContext.GetSingle(role.Name).Order != index)
            {
                await _rolesContext.Update(role.Id, x => x.Order, index);
            }
        }

        return _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order);
    }
}
