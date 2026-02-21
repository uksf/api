using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests.Commands;

public class UpdateUnitCommandHandlerTests
{
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();
    private readonly string _accountId1 = ObjectId.GenerateNewId().ToString();
    private readonly string _accountId2 = ObjectId.GenerateNewId().ToString();
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly UpdateUnitCommandHandler _subject;

    public UpdateUnitCommandHandlerTests()
    {
        _mockUnitsContext = new Mock<IUnitsContext>();
        _mockLogger = new Mock<IUksfLogger>();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockEventBus = new Mock<IEventBus>();

        _subject = new UpdateUnitCommandHandler(_mockUnitsContext.Object, _mockLogger.Object, _mockAccountContext.Object, _mockEventBus.Object);
    }

    [Fact]
    public async Task Should_update_unit_fields_via_context()
    {
        var oldUnit = CreateUnit("OldName", "OLD", "tsGroup1", "discordRole1");
        var updatedUnit = CreateUnit("NewName", "NEW", "tsGroup2", "discordRole2");
        updatedUnit.Members = new List<string>();
        var inputUnit = CreateUnit("NewName", "NEW", "tsGroup2", "discordRole2");

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockUnitsContext.Verify(x => x.Update(_unitId, It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Once);
    }

    [Fact]
    public async Task Should_log_audit_with_shortname()
    {
        var oldUnit = CreateUnit("Name", "SHORT", "ts1", "dc1");
        var updatedUnit = CreateUnit("Name", "SHORT", "ts1", "dc1");
        updatedUnit.Members = new List<string>();
        var inputUnit = CreateUnit("Name", "SHORT", "ts1", "dc1");

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(m => m.Contains("SHORT")), null), Times.Once);
    }

    [Fact]
    public async Task Should_update_account_unit_assignment_when_name_changes()
    {
        var oldUnit = CreateUnit("OldName", "SHORT", "ts1", "dc1");
        var updatedUnit = CreateUnit("NewName", "SHORT", "ts1", "dc1");
        updatedUnit.Members = new List<string>();
        var inputUnit = CreateUnit("NewName", "SHORT", "ts1", "dc1");

        var account1 = new DomainAccount { Id = _accountId1, UnitAssignment = "OldName" };
        var account2 = new DomainAccount { Id = _accountId2, UnitAssignment = "OldName" };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount> { account1, account2 });

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockAccountContext.Verify(
            x => x.Update(_accountId1, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), "NewName"),
            Times.Once
        );
        _mockAccountContext.Verify(
            x => x.Update(_accountId2, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), "NewName"),
            Times.Once
        );
    }

    [Fact]
    public async Task Should_not_update_account_unit_assignment_when_name_unchanged()
    {
        var oldUnit = CreateUnit("SameName", "SHORT", "ts1", "dc1");
        var updatedUnit = CreateUnit("SameName", "SHORT", "ts1", "dc1");
        updatedUnit.Members = new List<string>();
        var inputUnit = CreateUnit("SameName", "SHORT", "ts1", "dc1");

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockAccountContext.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Should_fire_event_for_each_member_when_teamspeak_group_changes()
    {
        var oldUnit = CreateUnit("Name", "SHORT", "tsOld", "dc1");
        var updatedUnit = CreateUnit("Name", "SHORT", "tsNew", "dc1");
        updatedUnit.Members = new List<string> { _accountId1, _accountId2 };
        var inputUnit = CreateUnit("Name", "SHORT", "tsNew", "dc1");

        var account1 = new DomainAccount { Id = _accountId1 };
        var account2 = new DomainAccount { Id = _accountId2 };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);
        _mockAccountContext.Setup(x => x.GetSingle(_accountId1)).Returns(account1);
        _mockAccountContext.Setup(x => x.GetSingle(_accountId2)).Returns(account2);

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventData>(), nameof(UpdateUnitCommandHandler)), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_fire_event_for_each_member_when_discord_role_changes()
    {
        var oldUnit = CreateUnit("Name", "SHORT", "ts1", "dcOld");
        var updatedUnit = CreateUnit("Name", "SHORT", "ts1", "dcNew");
        updatedUnit.Members = new List<string> { _accountId1 };
        var inputUnit = CreateUnit("Name", "SHORT", "ts1", "dcNew");

        var account1 = new DomainAccount { Id = _accountId1 };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);
        _mockAccountContext.Setup(x => x.GetSingle(_accountId1)).Returns(account1);

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventData>(), nameof(UpdateUnitCommandHandler)), Times.Once);
    }

    [Fact]
    public async Task Should_not_fire_events_when_neither_teamspeak_nor_discord_changed()
    {
        var oldUnit = CreateUnit("Name", "SHORT", "ts1", "dc1");
        var updatedUnit = CreateUnit("Name", "SHORT", "ts1", "dc1");
        updatedUnit.Members = new List<string> { _accountId1 };
        var inputUnit = CreateUnit("Name", "SHORT", "ts1", "dc1");

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(oldUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(updatedUnit);

        await _subject.ExecuteAsync(new UpdateUnitCommand(_unitId, inputUnit));

        _mockEventBus.Verify(x => x.Send(It.IsAny<EventData>(), It.IsAny<string>()), Times.Never);
    }

    private DomainUnit CreateUnit(string name, string shortname, string teamspeakGroup, string discordRoleId)
    {
        return new DomainUnit
        {
            Id = _unitId,
            Name = name,
            Shortname = shortname,
            TeamspeakGroup = teamspeakGroup,
            DiscordRoleId = discordRoleId,
            Members = new List<string>()
        };
    }
}
