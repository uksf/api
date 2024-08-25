using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class UnitsController(
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    IUnitsService unitsService,
    IAssignmentService assignmentService,
    INotificationsService notificationsService,
    IEventBus eventBus,
    IUksfLogger logger,
    IGetUnitTreeQuery getUnitTreeQuery,
    IUnitTreeMapper unitTreeMapper
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainUnit> Get([FromQuery] string filter = "", [FromQuery] string accountId = "")
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var response = filter switch
            {
                "auxiliary" => unitsService.GetSortedUnits(x => x.Branch == UnitBranch.Auxiliary && x.Members.Contains(accountId)),
                "available" => unitsService.GetSortedUnits(x => !x.Members.Contains(accountId)),
                _           => unitsService.GetSortedUnits(x => x.Members.Contains(accountId))
            };
            return response;
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var response = filter switch
            {
                "auxiliary" => unitsService.GetSortedUnits(x => x.Branch == UnitBranch.Auxiliary),
                "combat"    => unitsService.GetSortedUnits(x => x.Branch == UnitBranch.Combat),
                _           => unitsService.GetSortedUnits()
            };
            return response;
        }

        return unitsService.GetSortedUnits();
    }

    [HttpGet("{id}")]
    [Authorize]
    public UnitDto GetSingle([FromRoute] string id)
    {
        return unitsService.GetSingle(id);
    }

    [HttpGet("exists/{check}")]
    [Authorize]
    public bool GetUnitExists([FromRoute] string check, [FromQuery] string id = "")
    {
        if (string.IsNullOrEmpty(check))
        {
            return false;
        }

        var exists = unitsContext.GetSingle(
            x => (string.IsNullOrEmpty(id) || x.Id != id) &&
                 (string.Equals(x.Name, check, StringComparison.InvariantCultureIgnoreCase) ||
                  string.Equals(x.Shortname, check, StringComparison.InvariantCultureIgnoreCase) ||
                  string.Equals(x.TeamspeakGroup, check, StringComparison.InvariantCultureIgnoreCase) ||
                  string.Equals(x.DiscordRoleId, check, StringComparison.InvariantCultureIgnoreCase) ||
                  string.Equals(x.Callsign, check, StringComparison.InvariantCultureIgnoreCase))
        ) is not null;
        return exists;
    }

    [HttpGet("tree")]
    [Authorize]
    public async Task<UnitTreeDto> GetTree()
    {
        var combatTree = await getUnitTreeQuery.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Combat));
        var auxiliaryTree = await getUnitTreeQuery.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Auxiliary));
        return new UnitTreeDto
        {
            CombatNodes = new List<UnitTreeNodeDto> { unitTreeMapper.MapUnitTree(combatTree) },
            AuxiliaryNodes = new List<UnitTreeNodeDto> { unitTreeMapper.MapUnitTree(auxiliaryTree) }
        };
    }

    [HttpPost]
    [Authorize]
    public async Task AddUnit([FromBody] DomainUnit unit)
    {
        await unitsContext.Add(unit);
        logger.LogAudit($"New unit added: '{unit}'");
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task EditUnit([FromRoute] string id, [FromBody] DomainUnit unit)
    {
        var oldUnit = unitsContext.GetSingle(x => x.Id == id);
        await unitsContext.Replace(unit);
        logger.LogAudit($"Unit '{unit.Shortname}' updated: {oldUnit.Changes(unit)}");

        // TODO: Move this elsewhere
        unit = unitsContext.GetSingle(unit.Id);
        if (unit.Name != oldUnit.Name)
        {
            foreach (var account in accountContext.Get(x => x.UnitAssignment == oldUnit.Name))
            {
                await accountContext.Update(account.Id, x => x.UnitAssignment, unit.Name);
            }
        }

        if (unit.TeamspeakGroup != oldUnit.TeamspeakGroup || unit.DiscordRoleId != oldUnit.DiscordRoleId)
        {
            foreach (var account in unit.Members.Select(accountContext.GetSingle))
            {
                eventBus.Send(new ContextEventData<DomainAccount>(id, account), nameof(EditUnit));
            }
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task DeleteUnit([FromRoute] string id)
    {
        var unit = unitsContext.GetSingle(id);
        logger.LogAudit($"Unit deleted '{unit.Name}'");
        foreach (var account in accountContext.Get(x => x.UnitAssignment == unit.Name))
        {
            var notification = await assignmentService.UpdateUnitRankAndRole(account.Id, "Reserves", reason: $"{unit.Name} was deleted");
            notificationsService.Add(notification);
        }

        await unitsContext.Delete(id);
    }

    [HttpPatch("{id}/parent")]
    [Authorize]
    public async Task UpdateParent([FromRoute] string id, [FromBody] UnitUpdateParentDto unitUpdate)
    {
        var unit = unitsContext.GetSingle(id);
        var parentUnit = unitsContext.GetSingle(unitUpdate.ParentId);
        if (unit.Parent == parentUnit.Id)
        {
            return;
        }

        await unitsContext.Update(id, x => x.Parent, parentUnit.Id);
        if (unit.Branch != parentUnit.Branch)
        {
            await unitsContext.Update(id, x => x.Branch, parentUnit.Branch);
        }

        var parentChildren = unitsContext.Get(x => x.Parent == parentUnit.Id).ToList();
        parentChildren.Remove(parentChildren.FirstOrDefault(x => x.Id == unit.Id));
        parentChildren.Insert(unitUpdate.Index, unit);
        foreach (var child in parentChildren)
        {
            await unitsContext.Update(child.Id, x => x.Order, parentChildren.IndexOf(child));
        }

        unit = unitsContext.GetSingle(unit.Id);
        foreach (var child in unitsService.GetAllChildren(unit, true))
        {
            foreach (var accountId in child.Members)
            {
                assignmentService.UpdateGroupsAndRoles(accountId);
            }
        }
    }

    [HttpPatch("{id}/order")]
    [Authorize]
    public void UpdateSortOrder([FromRoute] string id, [FromBody] UnitUpdateOrderDto unitUpdate)
    {
        var unit = unitsContext.GetSingle(id);
        var parentUnit = unitsContext.GetSingle(x => x.Id == unit.Parent);
        var parentChildren = unitsContext.Get(x => x.Parent == parentUnit.Id).ToList();
        parentChildren.Remove(parentChildren.FirstOrDefault(x => x.Id == unit.Id));
        parentChildren.Insert(unitUpdate.Index, unit);
        foreach (var child in parentChildren)
        {
            unitsContext.Update(child.Id, x => x.Order, parentChildren.IndexOf(child));
        }
    }

    [HttpGet("chart/{type}")]
    [Authorize]
    public UnitChartNodeDto GetUnitsChart([FromRoute] string type)
    {
        switch (type)
        {
            case "combat":
                var combatRoot = unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Combat);
                return new UnitChartNodeDto
                {
                    Id = combatRoot.Id,
                    Name = combatRoot.PreferShortname ? combatRoot.Shortname : combatRoot.Name,
                    Members = unitsService.MapUnitMembers(combatRoot),
                    Children = GetUnitChartChildren(combatRoot.Id)
                };
            case "auxiliary":
                var auxiliaryRoot = unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Auxiliary);
                return new UnitChartNodeDto
                {
                    Id = auxiliaryRoot.Id,
                    Name = auxiliaryRoot.PreferShortname ? auxiliaryRoot.Shortname : auxiliaryRoot.Name,
                    Members = unitsService.MapUnitMembers(auxiliaryRoot),
                    Children = GetUnitChartChildren(auxiliaryRoot.Id)
                };
            default: throw new BadRequestException("Invalid chart type");
        }
    }

    private IEnumerable<UnitChartNodeDto> GetUnitChartChildren(string parent)
    {
        return unitsContext.Get(x => x.Parent == parent)
                           .Select(
                               unit => new UnitChartNodeDto
                               {
                                   Id = unit.Id,
                                   Name = unit.PreferShortname ? unit.Shortname : unit.Name,
                                   Members = unitsService.MapUnitMembers(unit),
                                   Children = GetUnitChartChildren(unit.Id)
                               }
                           );
    }
}
