using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Mappers;

public interface IUnitMapper
{
    UnitDto Map(DomainUnit unit, string code, string parentName, IEnumerable<UnitMemberDto> unitMembers);
}

public class UnitMapper : IUnitMapper
{
    public UnitDto Map(DomainUnit unit, string code, string parentName, IEnumerable<UnitMemberDto> unitMembers)
    {
        return new UnitDto
        {
            Id = unit.Id,
            Branch = unit.Branch,
            Callsign = unit.Callsign,
            ChainOfCommand = unit.ChainOfCommand,
            DiscordRoleId = unit.DiscordRoleId,
            Icon = unit.Icon,
            Members = unit.Members,
            Name = unit.Name,
            Order = unit.Order,
            Parent = unit.Parent,
            PreferShortname = unit.PreferShortname,
            Shortname = unit.Shortname,
            TeamspeakGroup = unit.TeamspeakGroup,
            Code = code,
            ParentName = parentName,
            UnitMembers = unitMembers
        };
    }
}
