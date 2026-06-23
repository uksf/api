using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests.Services;

public class DiscordMembersServiceTests
{
    private readonly Mock<IRanksContext> _mockRanksContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IUnitsService> _mockUnitsService = new();
    private readonly DiscordMembersService _subject;
    private readonly List<DomainUnit> _units = [];

    public DiscordMembersServiceTests()
    {
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns((Func<DomainUnit, bool> predicate) => _units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((Func<DomainUnit, bool> predicate) => _units.FirstOrDefault(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns((string id) => _units.FirstOrDefault(x => x.Id == id));
        _mockUnitsService.Setup(x => x.GetParents(It.IsAny<DomainUnit>())).Returns(new List<DomainUnit>());

        _subject = new DiscordMembersService(
            new Mock<IDiscordClientService>().Object,
            new Mock<IHttpContextService>().Object,
            new Mock<IVariablesService>().Object,
            new Mock<IAccountContext>().Object,
            _mockUnitsContext.Object,
            _mockRanksContext.Object,
            _mockUnitsService.Object,
            new Mock<IDisplayNameService>().Object,
            new Mock<IUksfLogger>().Object
        );
    }

    private DomainUnit AddUnit(string name, string discordRoleId, string parent = null)
    {
        var unit = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = name,
            DiscordRoleId = discordRoleId,
            Parent = parent
        };
        _units.Add(unit);
        return unit;
    }

    [Fact]
    public void When_account_is_member_of_a_unit_with_a_discord_role()
    {
        var account = new DomainAccount { Id = ObjectId.GenerateNewId().ToString(), UnitAssignment = "SFM" };
        var sfm = AddUnit("SFM", "111");
        sfm.Members.Add(account.Id);

        HashSet<string> allowedRoles = [];
        _subject.UpdateAccountUnits(account, allowedRoles);

        allowedRoles.Should().Contain("111");
    }

    [Fact]
    public void When_account_unit_has_parents_with_discord_roles()
    {
        var account = new DomainAccount { Id = ObjectId.GenerateNewId().ToString(), UnitAssignment = "SFM" };
        var uksf = AddUnit("UKSF", "999");
        var sfm = AddUnit("SFM", "111", uksf.Id);
        sfm.Members.Add(account.Id);
        _mockUnitsService.Setup(x => x.GetParents(It.Is<DomainUnit>(u => u.Name == "SFM"))).Returns([sfm, uksf]);

        HashSet<string> allowedRoles = [];
        _subject.UpdateAccountUnits(account, allowedRoles);

        allowedRoles.Should().Contain("999");
    }

    [Fact]
    public void When_account_is_attached_to_a_troop_should_add_troop_discord_role()
    {
        var account = new DomainAccount { Id = ObjectId.GenerateNewId().ToString(), UnitAssignment = "SFM" };
        AddUnit("SFM", "111").Members.Add(account.Id);
        var airTroop = AddUnit("Air Troop", "387258057182281729");
        account.AttachedTroop = airTroop.Id;

        HashSet<string> allowedRoles = [];
        _subject.UpdateAccountUnits(account, allowedRoles);

        allowedRoles.Should().Contain("387258057182281729");
    }

    [Fact]
    public void When_account_is_not_attached_should_not_add_any_attachment_role()
    {
        var account = new DomainAccount { Id = ObjectId.GenerateNewId().ToString(), UnitAssignment = "SFM", AttachedTroop = null };
        AddUnit("SFM", "111").Members.Add(account.Id);

        HashSet<string> allowedRoles = [];
        _subject.UpdateAccountUnits(account, allowedRoles);

        allowedRoles.Should().BeEquivalentTo(["111"]);
    }

    [Fact]
    public void When_attached_troop_has_no_discord_role_should_add_nothing()
    {
        var account = new DomainAccount { Id = ObjectId.GenerateNewId().ToString(), UnitAssignment = "SFM" };
        AddUnit("SFM", "111").Members.Add(account.Id);
        var troop = AddUnit("Roleless Troop", "");
        account.AttachedTroop = troop.Id;

        HashSet<string> allowedRoles = [];
        _subject.UpdateAccountUnits(account, allowedRoles);

        allowedRoles.Should().BeEquivalentTo(["111"]);
    }

    [Fact]
    public void When_attached_troop_does_not_exist_should_add_nothing()
    {
        var account = new DomainAccount
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UnitAssignment = "SFM",
            AttachedTroop = ObjectId.GenerateNewId().ToString()
        };
        AddUnit("SFM", "111").Members.Add(account.Id);

        HashSet<string> allowedRoles = [];
        _subject.UpdateAccountUnits(account, allowedRoles);

        allowedRoles.Should().BeEquivalentTo(["111"]);
    }
}
