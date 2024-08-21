using MongoDB.Bson;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Mappers;

public interface ICommandMemberMapper
{
    Account MapCommandMemberToAccount(CommandMemberAccount commandMemberAccount);
}

public class CommandMemberMapper : ICommandMemberMapper
{
    public Account MapCommandMemberToAccount(CommandMemberAccount commandMemberAccount)
    {
        return new Account
        {
            Id = commandMemberAccount.Id,
            Firstname = commandMemberAccount.Firstname,
            Lastname = commandMemberAccount.Lastname,
            Qualifications = commandMemberAccount.Qualifications,
            Trainings = commandMemberAccount.Trainings.Select(MapToTraining).ToList(),
            RankObject = MapToRank(commandMemberAccount.Rank),
            RoleObject = MapToRole(commandMemberAccount.Role),
            UnitObject = MapToUnitWithParentTree(commandMemberAccount.Unit, commandMemberAccount.ParentUnits),
            UnitObjects = commandMemberAccount.Units.OrderBy(x => x.Branch).Select(x => MapToUnit(x, commandMemberAccount.Id)).ToList()
        };
    }

    private static Training MapToTraining(DomainTraining training)
    {
        return new Training
        {
            Id = training.Id,
            Name = training.Name,
            ShortName = training.ShortName
        };
    }

    private static Rank MapToRank(DomainRank rank)
    {
        return new Rank
        {
            Id = rank.Id,
            Name = rank.Name,
            Abbreviation = rank.Abbreviation
        };
    }

    private static Role MapToRole(DomainRole role)
    {
        return new Role { Id = role.Id, Name = role.Name };
    }

    private static UnitTreeNodeDto MapToUnit(DomainUnit unit, string memberId)
    {
        return new UnitTreeNodeDto
        {
            Id = unit.Id,
            Name = unit.Name,
            Shortname = unit.Shortname,
            PreferShortname = unit.PreferShortname,
            MemberRole = unit.Roles.GetKeyFromValue(memberId)
        };
    }

    private static UnitTreeNodeDto MapToUnitWithParentTree(DomainUnit unit, List<DomainUnit> parents)
    {
        var parentUnit = unit.Parent == ObjectId.Empty.ToString() ? null : parents.FirstOrDefault(x => x.Id == unit.Parent);
        var parentNode = parentUnit == null ? null : MapToUnitWithParentTree(parentUnit, parents);
        return new UnitTreeNodeDto
        {
            Id = unit.Id,
            Order = unit.Order,
            Name = unit.Name,
            Shortname = unit.Shortname,
            PreferShortname = unit.PreferShortname,
            ParentUnit = parentNode
        };
    }
}
