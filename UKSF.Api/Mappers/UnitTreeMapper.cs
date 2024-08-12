using UKSF.Api.Core.Models;

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

    private static UnitTreeNodeDto MapUnit(DomainUnit domainUnit)
    {
        return new UnitTreeNodeDto
        {
            Id = domainUnit.Id,
            Order = domainUnit.Order,
            Name = domainUnit.Name,
            Shortname = domainUnit.Shortname,
            PreferShortname = domainUnit.PreferShortname,
            MemberIds = domainUnit.Members,
            Children = domainUnit.Children.Select(MapUnit).ToList()
        };
    }
}
