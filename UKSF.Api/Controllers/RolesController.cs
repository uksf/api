using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
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
    INotificationsService notificationsService,
    IUksfLogger logger
) : ControllerBase
{
    private static readonly List<DomainRole> ChainOfCommandPositions = new()
    {
        new DomainRole
        {
            Id = "chain-1ic",
            Name = "1iC",
            Order = 0,
            RoleType = RoleType.Unit
        },
        new DomainRole
        {
            Id = "chain-2ic",
            Name = "2iC",
            Order = 1,
            RoleType = RoleType.Unit
        },
        new DomainRole
        {
            Id = "chain-3ic",
            Name = "3iC",
            Order = 2,
            RoleType = RoleType.Unit
        },
        new DomainRole
        {
            Id = "chain-ncoic",
            Name = "NCOiC",
            Order = 3,
            RoleType = RoleType.Unit
        }
    };

    [HttpGet]
    [Authorize]
    public RolesDataset GetRoles([FromQuery] string id = "", [FromQuery] string unitId = "")
    {
        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(unitId))
        {
            var unit = unitsContext.GetSingle(unitId);
            var availablePositions = GetAvailableChainOfCommandPositions(unit, id);
            return new RolesDataset { UnitRoles = availablePositions };
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
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual), UnitRoles = ChainOfCommandPositions.OrderBy(x => x.Order)
        };
    }

    private IEnumerable<DomainRole> GetAvailableChainOfCommandPositions(DomainUnit unit, string memberId)
    {
        var chainOfCommand = unit.ChainOfCommand ?? new ChainOfCommand();

        // Return positions that are empty (not the member's current position)
        return ChainOfCommandPositions.Where(position =>
            {
                return position.Name switch
                {
                    "1iC"   => string.IsNullOrEmpty(chainOfCommand.OneIC),
                    "2iC"   => string.IsNullOrEmpty(chainOfCommand.TwoIC),
                    "3iC"   => string.IsNullOrEmpty(chainOfCommand.ThreeIC),
                    "NCOiC" => string.IsNullOrEmpty(chainOfCommand.NCOIC),
                    _       => false
                };
            }
        );
    }

    [HttpPost("{roleType}/{check}")]
    [Authorize]
    public DomainRole CheckRole([FromRoute] RoleType roleType, [FromRoute] string check, [FromBody] DomainRole role = null)
    {
        if (string.IsNullOrEmpty(check))
        {
            return null;
        }

        // For unit roles, check against hardcoded chain of command positions
        if (roleType == RoleType.Unit)
        {
            var chainPosition = ChainOfCommandPositions.FirstOrDefault(x => x.Name == check);
            if (chainPosition != null && role?.Id != chainPosition.Id)
            {
                return chainPosition;
            }

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
        // Don't allow adding unit roles as they are hardcoded chain of command positions
        if (role.RoleType == RoleType.Unit)
        {
            throw new BadRequestException("Unit roles are managed through the chain of command system");
        }

        await rolesContext.Add(role);
        logger.LogAudit($"Role added '{role.Name}'");
        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual), UnitRoles = ChainOfCommandPositions.OrderBy(x => x.Order)
        };
    }

    [HttpPatch]
    [Authorize]
    public async Task<RolesDataset> EditRole([FromBody] DomainRole role)
    {
        // Don't allow editing unit roles as they are hardcoded chain of command positions
        if (role.RoleType == RoleType.Unit)
        {
            throw new BadRequestException("Unit roles are managed through the chain of command system");
        }

        var oldRole = rolesContext.GetSingle(x => x.Id == role.Id);
        logger.LogAudit($"Role updated from '{oldRole.Name}' to '{role.Name}'");
        await rolesContext.Update(role.Id, x => x.Name, role.Name);
        foreach (var account in accountContext.Get(x => x.RoleAssignment == oldRole.Name))
        {
            await accountContext.Update(account.Id, x => x.RoleAssignment, role.Name);
        }

        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual), UnitRoles = ChainOfCommandPositions.OrderBy(x => x.Order)
        };
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<RolesDataset> DeleteRole([FromRoute] string id)
    {
        // Don't allow deleting unit roles as they are hardcoded chain of command positions
        var chainPosition = ChainOfCommandPositions.FirstOrDefault(x => x.Id == id);
        if (chainPosition != null)
        {
            throw new BadRequestException("Unit roles are managed through the chain of command system");
        }

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

        return new RolesDataset
        {
            IndividualRoles = rolesContext.Get(x => x.RoleType == RoleType.Individual), UnitRoles = ChainOfCommandPositions.OrderBy(x => x.Order)
        };
    }

    [HttpPost("order")]
    [Authorize]
    public async Task<IOrderedEnumerable<DomainRole>> UpdateOrder([FromBody] List<DomainRole> newRoleOrder)
    {
        // Unit roles have fixed order in chain of command, only allow reordering individual roles
        var individualRoles = newRoleOrder.Where(x => x.RoleType == RoleType.Individual).ToList();
        for (var index = 0; index < individualRoles.Count; index++)
        {
            var role = individualRoles[index];
            if (rolesContext.GetSingle(role.Name).Order != index)
            {
                await rolesContext.Update(role.Id, x => x.Order, index);
            }
        }

        return ChainOfCommandPositions.OrderBy(x => x.Order);
    }
}
