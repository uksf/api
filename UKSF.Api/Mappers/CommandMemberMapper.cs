using MongoDB.Bson;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Mappers;

public interface ICommandMemberMapper
{
    Account MapCommandMemberToAccount(DomainCommandMember domainCommandMember);
}

public class CommandMemberMapper : ICommandMemberMapper
{
    public Account MapCommandMemberToAccount(DomainCommandMember domainCommandMember)
    {
        return new()
        {
            Id = domainCommandMember.Id,
            Firstname = domainCommandMember.Firstname,
            Lastname = domainCommandMember.Lastname,
            Qualifications = domainCommandMember.Qualifications,
            RankObject = MapToRank(domainCommandMember.Rank),
            RoleObject = MapToRole(domainCommandMember.Role),
            UnitObject = MapToUnitWithParentTree(domainCommandMember.Unit, domainCommandMember.ParentUnits),
            UnitObjects = domainCommandMember.Units.OrderBy(x => x.Branch).Select(x => MapToUnit(x, domainCommandMember.Id)).ToList()
        };
    }

    private static Rank MapToRank(DomainRank domainRank)
    {
        return new() { Id = domainRank.Id, Name = domainRank.Name, Abbreviation = domainRank.Abbreviation };
    }

    private static Role MapToRole(DomainRole domainRole)
    {
        return new() { Id = domainRole.Id, Name = domainRole.Name };
    }

    private static Unit MapToUnit(DomainUnit domainUnit, string memberId)
    {
        return new()
        {
            Id = domainUnit.Id,
            Name = domainUnit.Name,
            Shortname = domainUnit.Shortname,
            PreferShortname = domainUnit.PreferShortname,
            MemberRole = domainUnit.Roles.GetKeyFromValue(memberId)
        };
    }

    private static Unit MapToUnitWithParentTree(DomainUnit domainUnit, List<DomainUnit> parents)
    {
        var domainParent = domainUnit.Parent == ObjectId.Empty.ToString() ? null : parents.FirstOrDefault(x => x.Id == domainUnit.Parent);
        var parent = domainParent == null ? null : MapToUnitWithParentTree(domainParent, parents);
        return new()
        {
            Id = domainUnit.Id,
            Order = domainUnit.Order,
            Name = domainUnit.Name,
            Shortname = domainUnit.Shortname,
            PreferShortname = domainUnit.PreferShortname,
            ParentUnit = parent
        };
    }
}
