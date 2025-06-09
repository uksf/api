using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Commands;

public record UpdateUnitCommand(string UnitId, DomainUnit Unit);

public interface IUpdateUnitCommandHandler
{
    Task ExecuteAsync(UpdateUnitCommand command);
}

public class UpdateUnitCommandHandler(IUnitsContext unitsContext, IUksfLogger logger, IAccountContext accountContext, IEventBus eventBus)
    : IUpdateUnitCommandHandler
{
    public async Task ExecuteAsync(UpdateUnitCommand command)
    {
        var oldUnit = unitsContext.GetSingle(x => x.Id == command.UnitId);
        await unitsContext.Update(
            command.UnitId,
            Builders<DomainUnit>.Update.Set(x => x.Name, command.Unit.Name)
                                .Set(x => x.Shortname, command.Unit.Shortname)
                                .Set(x => x.Parent, command.Unit.Parent)
                                .Set(x => x.Branch, command.Unit.Branch)
                                .Set(x => x.TeamspeakGroup, command.Unit.TeamspeakGroup)
                                .Set(x => x.DiscordRoleId, command.Unit.DiscordRoleId)
                                .Set(x => x.Callsign, command.Unit.Callsign)
                                .Set(x => x.Icon, command.Unit.Icon)
                                .Set(x => x.PreferShortname, command.Unit.PreferShortname)
        );
        var updatedUnit = unitsContext.GetSingle(command.Unit.Id);
        logger.LogAudit($"Unit '{command.Unit.Shortname}' updated: {oldUnit.Changes(updatedUnit)}");

        await UpdateAccounts(updatedUnit, oldUnit);
    }

    private async Task UpdateAccounts(DomainUnit unit, DomainUnit oldUnit)
    {
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
                eventBus.Send(new ContextEventData<DomainAccount>(unit.Id, account), nameof(UpdateUnitCommandHandler));
            }
        }
    }
}
