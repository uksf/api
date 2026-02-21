using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class AssignmentServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IServiceRecordService> _mockServiceRecordService = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<IUnitsService> _mockUnitsService = new();
    private readonly Mock<IChainOfCommandService> _mockChainOfCommandService = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly AssignmentService _subject;

    private const string MemberId = "member1";

    public AssignmentServiceTests()
    {
        _subject = new AssignmentService(
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockServiceRecordService.Object,
            _mockRanksService.Object,
            _mockUnitsService.Object,
            _mockChainOfCommandService.Object,
            _mockDisplayNameService.Object,
            _mockEventBus.Object
        );
    }

    private DomainAccount CreateAccount(string rank = null, string unitAssignment = null, string roleAssignment = null)
    {
        return new DomainAccount
        {
            Id = MemberId,
            Rank = rank,
            UnitAssignment = unitAssignment,
            RoleAssignment = roleAssignment,
            Firstname = "John",
            Lastname = "Doe"
        };
    }

    private DomainUnit CreateUnit(string name, UnitBranch branch = UnitBranch.Combat, string id = null)
    {
        return new DomainUnit
        {
            Id = id ?? $"unit-{name}",
            Name = name,
            Branch = branch,
            Members = new List<string>()
        };
    }

    private void SetupAccountContext(DomainAccount account)
    {
        _mockAccountContext.Setup(x => x.GetSingle(MemberId)).Returns(account);
    }

    private void SetupUnitLookup(DomainUnit unit)
    {
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => predicate(unit) ? unit : null);
    }

    private void SetupUnitLookupFromList(params DomainUnit[] units)
    {
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
    }

    #region UpdateUnitRankAndRole

    [Fact]
    public async Task UpdateUnitRankAndRole_No_Changes_Returns_Null()
    {
        var account = CreateAccount();
        SetupAccountContext(account);

        var result = await _subject.UpdateUnitRankAndRole(MemberId);

        result.Should().BeNull();
        _mockServiceRecordService.Verify(x => x.AddServiceRecord(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Unit_Transfer_Combat_Branch()
    {
        var account = CreateAccount(unitAssignment: "OldUnit");
        SetupAccountContext(account);
        var newUnit = CreateUnit("NewUnit", UnitBranch.Combat, "newUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("1 Platoon > Alpha Squad");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "NewUnit");

        result.Should().NotBeNull();
        result.Owner.Should().Be(MemberId);
        result.Message.Should().Contain("You have been transferred to 1 Platoon > Alpha Squad");
        result.Icon.Should().Be(NotificationIcons.Promotion);
        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, "OldUnit"), Times.Once);
        _mockAccountContext.Verify(x => x.Update(MemberId, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), "NewUnit"), Times.Once);
        _mockUnitsService.Verify(x => x.AddMember(MemberId, "newUnitId"), Times.Once);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Unit_Transfer_Non_Combat_Branch_Does_Not_Update_UnitAssignment()
    {
        var account = CreateAccount(unitAssignment: "OldUnit");
        SetupAccountContext(account);
        var newUnit = CreateUnit("AuxUnit", UnitBranch.Auxiliary, "auxUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("Auxiliary > AuxUnit");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "AuxUnit");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been transferred to Auxiliary > AuxUnit");
        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, "OldUnit"), Times.Never);
        _mockAccountContext.Verify(
            x => x.Update(MemberId, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), "AuxUnit"),
            Times.Never
        );
        _mockUnitsService.Verify(x => x.AddMember(MemberId, "auxUnitId"), Times.Once);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Unit_Remove_With_Existing_Unit()
    {
        var account = CreateAccount(unitAssignment: "CurrentUnit");
        SetupAccountContext(account);
        var currentUnit = CreateUnit("CurrentUnit", UnitBranch.Combat, "currentUnitId");
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate =>
                             {
                                 if (predicate(currentUnit))
                                 {
                                     return currentUnit;
                                 }

                                 return null;
                             }
                         );
        _mockUnitsService.Setup(x => x.GetChainString(currentUnit)).Returns("1 Platoon > CurrentUnit");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: AssignmentService.RemoveFlag);

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been removed from 1 Platoon > CurrentUnit");
        result.Icon.Should().Be(NotificationIcons.Promotion);
        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, "CurrentUnit"), Times.Once);
        _mockAccountContext.Verify(
            x => x.Update(MemberId, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), (string)null),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Unit_Remove_With_No_Current_Unit_Returns_Null()
    {
        var account = CreateAccount(unitAssignment: null);
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: AssignmentService.RemoveFlag);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Role_Assignment()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, role: "Rifleman");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been assigned as a Rifleman");
        result.Icon.Should().Be(NotificationIcons.Promotion);
        _mockAccountContext.Verify(
            x => x.Update(MemberId, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), "Rifleman"),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Role_Assignment_After_Unit_Transfer()
    {
        var account = CreateAccount(unitAssignment: "OldUnit");
        SetupAccountContext(account);
        var newUnit = CreateUnit("NewUnit", UnitBranch.Combat, "newUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("NewUnit");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "NewUnit", role: "Rifleman");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been transferred to NewUnit as a Rifleman");
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Role_Remove_With_Existing_Role()
    {
        var account = CreateAccount(roleAssignment: "Medic");
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, role: AssignmentService.RemoveFlag);

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been unassigned as a Medic");
        result.Icon.Should().Be(NotificationIcons.Promotion);
        _mockAccountContext.Verify(
            x => x.Update(MemberId, It.IsAny<System.Linq.Expressions.Expression<Func<DomainAccount, string>>>(), (string)null),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Role_Remove_With_No_Current_Role()
    {
        var account = CreateAccount(roleAssignment: null);
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, role: AssignmentService.RemoveFlag);

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been unassigned from your role");
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Role_Remove_After_Unit_Transfer()
    {
        var account = CreateAccount(unitAssignment: "OldUnit", roleAssignment: "Medic");
        SetupAccountContext(account);
        var newUnit = CreateUnit("NewUnit", UnitBranch.Combat, "newUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("NewUnit");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "NewUnit", role: AssignmentService.RemoveFlag);

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been transferred to NewUnit and unassigned as a Medic");
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Promotion()
    {
        var account = CreateAccount(rank: "Private");
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);
        _mockRanksService.Setup(x => x.IsSuperior("Corporal", "Private")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Cpl.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, rankString: "Corporal");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been promoted to Corporal");
        result.Message.Should().Contain("Please change your TeamSpeak and Arma name to Cpl.J.Doe");
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Promotion_From_Null()
    {
        var account = CreateAccount(rank: null);
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Pvt.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, rankString: "Private");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been promoted to Private");
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Demotion()
    {
        var account = CreateAccount(rank: "Corporal");
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);
        _mockRanksService.Setup(x => x.IsSuperior("Private", "Corporal")).Returns(false);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Pvt.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, rankString: "Private");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been demoted to Private");
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Same_As_Current_Returns_Null()
    {
        var account = CreateAccount(rank: "Private");
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, rankString: "Private");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Remove()
    {
        var account = CreateAccount(rank: "Corporal");
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, rankString: AssignmentService.RemoveFlag);

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been demoted from Corporal");
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Combined_Unit_Role_Rank()
    {
        var account = CreateAccount(rank: "Private", unitAssignment: "OldUnit");
        SetupAccountContext(account);
        var newUnit = CreateUnit("NewUnit", UnitBranch.Combat, "newUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("NewUnit");
        _mockRanksService.Setup(x => x.IsSuperior("Corporal", "Private")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Cpl.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "NewUnit", role: "Rifleman", rankString: "Corporal");

        result.Should().NotBeNull();
        result.Message.Should().Contain("You have been transferred to NewUnit");
        result.Message.Should().Contain("as a Rifleman");
        result.Message.Should().Contain("and promoted to Corporal");
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Custom_Message_Overrides_Generated()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, role: "Rifleman", message: "Custom message here");

        result.Should().NotBeNull();
        result.Message.Should().Be("Custom message here");
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Reason_Appended()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, role: "Rifleman", reason: "outstanding performance");

        result.Should().NotBeNull();
        result.Message.Should().Contain("because outstanding performance");
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Update_Appends_Name_Change_Reminder()
    {
        var account = CreateAccount(rank: "Private");
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);
        _mockRanksService.Setup(x => x.IsSuperior("Corporal", "Private")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Cpl.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, rankString: "Corporal");

        result.Should().NotBeNull();
        result.Message.Should().Contain("Please change your TeamSpeak and Arma name to Cpl.J.Doe");
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Service_Record_Created()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        await _subject.UpdateUnitRankAndRole(MemberId, role: "Rifleman", notes: "Test notes");

        _mockServiceRecordService.Verify(x => x.AddServiceRecord(MemberId, It.IsAny<string>(), "Test notes"), Times.Once);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Calls_UpdateGroupsAndRoles()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        await _subject.UpdateUnitRankAndRole(MemberId, role: "Rifleman");

        _mockEventBus.Verify(x => x.Send(It.IsAny<ContextEventData<DomainAccount>>(), nameof(AssignmentService)), Times.Once);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Message_Is_RemoveFlag_Returns_Null()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        var result = await _subject.UpdateUnitRankAndRole(MemberId, role: "Rifleman", message: AssignmentService.RemoveFlag);

        result.Should().BeNull();
        _mockServiceRecordService.Verify(x => x.AddServiceRecord(MemberId, AssignmentService.RemoveFlag, ""), Times.Once);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Demotion_With_Positive_Unit_Transfer_Is_Positive()
    {
        var account = CreateAccount(rank: "Corporal", unitAssignment: "OldUnit");
        SetupAccountContext(account);
        var newUnit = CreateUnit("NewUnit", UnitBranch.Combat, "newUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("NewUnit");
        _mockRanksService.Setup(x => x.IsSuperior("Private", "Corporal")).Returns(false);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Pvt.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "NewUnit", rankString: "Private");

        result.Should().NotBeNull();
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Rank_Promotion_Overrides_Negative_Unit()
    {
        var account = CreateAccount(rank: "Private", unitAssignment: "CurrentUnit");
        SetupAccountContext(account);
        var currentUnit = CreateUnit("CurrentUnit", UnitBranch.Combat, "currentUnitId");
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate =>
                             {
                                 if (predicate(currentUnit))
                                 {
                                     return currentUnit;
                                 }

                                 return null;
                             }
                         );
        _mockUnitsService.Setup(x => x.GetChainString(currentUnit)).Returns("CurrentUnit");
        _mockRanksService.Setup(x => x.IsSuperior("Corporal", "Private")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(MemberId)).Returns("Cpl.J.Doe");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: AssignmentService.RemoveFlag, rankString: "Corporal");

        result.Should().NotBeNull();
        result.Icon.Should().Be(NotificationIcons.Promotion);
    }

    [Fact]
    public async Task UpdateUnitRankAndRole_Role_Remove_After_Unit_Transfer_Uses_And_Prefix()
    {
        var account = CreateAccount(unitAssignment: "OldUnit", roleAssignment: null);
        SetupAccountContext(account);
        var newUnit = CreateUnit("NewUnit", UnitBranch.Combat, "newUnitId");
        SetupUnitLookup(newUnit);
        _mockUnitsService.Setup(x => x.GetChainString(newUnit)).Returns("NewUnit");

        var result = await _subject.UpdateUnitRankAndRole(MemberId, unitString: "NewUnit", role: AssignmentService.RemoveFlag);

        result.Should().NotBeNull();
        result.Message.Should().Contain("and unassigned from your role");
    }

    #endregion

    #region AssignUnitChainOfCommandPosition

    [Fact]
    public async Task AssignUnitChainOfCommandPosition_Calls_SetMemberChainOfCommandPosition_And_UpdateGroupsAndRoles()
    {
        var account = CreateAccount();
        SetupAccountContext(account);

        await _subject.AssignUnitChainOfCommandPosition(MemberId, "unit1", "1iC");

        _mockChainOfCommandService.Verify(x => x.SetMemberChainOfCommandPosition(MemberId, "unit1", "1iC"), Times.Once);
        _mockEventBus.Verify(x => x.Send(It.IsAny<ContextEventData<DomainAccount>>(), nameof(AssignmentService)), Times.Once);
    }

    #endregion

    #region UnassignAllUnits

    [Fact]
    public async Task UnassignAllUnits_Removes_From_All_Units_And_Calls_UpdateGroupsAndRoles()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        var unit1 = CreateUnit("Unit1", id: "u1");
        var unit2 = CreateUnit("Unit2", id: "u2");
        var unit3 = CreateUnit("Unit3", id: "u3");
        _mockUnitsContext.Setup(x => x.Get())
        .Returns(
            new List<DomainUnit>
            {
                unit1,
                unit2,
                unit3
            }
        );

        await _subject.UnassignAllUnits(MemberId);

        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, unit1), Times.Once);
        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, unit2), Times.Once);
        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, unit3), Times.Once);
        _mockEventBus.Verify(x => x.Send(It.IsAny<ContextEventData<DomainAccount>>(), nameof(AssignmentService)), Times.Once);
    }

    #endregion

    #region UnassignAllUnitChainOfCommandPositions

    [Fact]
    public async Task UnassignAllUnitChainOfCommandPositions_Clears_All_Units_And_Calls_UpdateGroupsAndRoles()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        var unit1 = CreateUnit("Unit1", id: "u1");
        var unit2 = CreateUnit("Unit2", id: "u2");
        _mockUnitsContext.Setup(x => x.Get()).Returns(new List<DomainUnit> { unit1, unit2 });

        await _subject.UnassignAllUnitChainOfCommandPositions(MemberId);

        _mockChainOfCommandService.Verify(x => x.SetMemberChainOfCommandPosition(MemberId, unit1, ""), Times.Once);
        _mockChainOfCommandService.Verify(x => x.SetMemberChainOfCommandPosition(MemberId, unit2, ""), Times.Once);
        _mockEventBus.Verify(x => x.Send(It.IsAny<ContextEventData<DomainAccount>>(), nameof(AssignmentService)), Times.Once);
    }

    #endregion

    #region UnassignUnitChainOfCommandPosition

    [Fact]
    public async Task UnassignUnitChainOfCommandPosition_Member_Has_Position_Returns_Position_Name()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        var unit = CreateUnit("TestUnit", id: "unitId");
        unit.ChainOfCommand = new ChainOfCommand { First = MemberId };
        _mockUnitsContext.Setup(x => x.GetSingle("unitId")).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(unit, MemberId)).Returns(true);

        var result = await _subject.UnassignUnitChainOfCommandPosition(MemberId, "unitId");

        result.Should().Be("1iC");
        _mockChainOfCommandService.Verify(x => x.SetMemberChainOfCommandPosition(MemberId, "unitId", ""), Times.Once);
        _mockEventBus.Verify(x => x.Send(It.IsAny<ContextEventData<DomainAccount>>(), nameof(AssignmentService)), Times.Once);
    }

    [Fact]
    public async Task UnassignUnitChainOfCommandPosition_Member_Not_In_Chain_Returns_Empty()
    {
        var unit = CreateUnit("TestUnit", id: "unitId");
        unit.ChainOfCommand = new ChainOfCommand();
        _mockUnitsContext.Setup(x => x.GetSingle("unitId")).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(unit, MemberId)).Returns(false);

        var result = await _subject.UnassignUnitChainOfCommandPosition(MemberId, "unitId");

        result.Should().BeEmpty();
        _mockChainOfCommandService.Verify(x => x.SetMemberChainOfCommandPosition(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockEventBus.Verify(x => x.Send(It.IsAny<EventData>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region UnassignUnit

    [Fact]
    public async Task UnassignUnit_Gets_Unit_And_Removes_Member()
    {
        var account = CreateAccount();
        SetupAccountContext(account);
        var unit = CreateUnit("TestUnit", id: "unitId");
        _mockUnitsContext.Setup(x => x.GetSingle("unitId")).Returns(unit);

        await _subject.UnassignUnit(MemberId, "unitId");

        _mockUnitsService.Verify(x => x.RemoveMember(MemberId, unit), Times.Once);
        _mockEventBus.Verify(x => x.Send(It.IsAny<ContextEventData<DomainAccount>>(), nameof(AssignmentService)), Times.Once);
    }

    #endregion

    #region UpdateGroupsAndRoles

    [Fact]
    public void UpdateGroupsAndRoles_Sends_ContextEventData_To_EventBus()
    {
        var account = CreateAccount();
        SetupAccountContext(account);

        _subject.UpdateGroupsAndRoles(MemberId);

        _mockEventBus.Verify(
            x => x.Send(It.Is<ContextEventData<DomainAccount>>(e => e.Id == MemberId && e.Data == account), nameof(AssignmentService)),
            Times.Once
        );
    }

    #endregion
}
