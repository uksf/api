﻿using UKSF.Api.Shared.Models;

namespace UKSF.Api.Mappers;

public interface IUnitTreeMapper
{
    Unit MapUnitTree(DomainUnit rootUnit);
}

public class UnitTreeMapper : IUnitTreeMapper
{
    public Unit MapUnitTree(DomainUnit rootUnit)
    {
        return MapUnit(rootUnit);
    }

    private static Unit MapUnit(DomainUnit domainUnit)
    {
        return new()
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