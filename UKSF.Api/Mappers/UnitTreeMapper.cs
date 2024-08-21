using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Mappers;

public interface IUnitTreeMapper
{
    UnitTreeNodeDto MapUnitTree(DomainUnit rootUnit);
}

public class UnitTreeMapper : IUnitTreeMapper
{
    public UnitTreeNodeDto MapUnitTree(DomainUnit rootUnit)
    {
        return MapUnit(rootUnit);
    }

    private static UnitTreeNodeDto MapUnit(DomainUnit unit)
    {
        return new UnitTreeNodeDto
        {
            Id = unit.Id,
            Order = unit.Order,
            Name = unit.Name,
            Shortname = unit.Shortname,
            PreferShortname = unit.PreferShortname,
            MemberIds = unit.Members,
            Children = unit.Children.Select(MapUnit).ToList()
        };
    }
}
