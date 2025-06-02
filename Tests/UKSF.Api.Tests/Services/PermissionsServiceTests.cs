using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class PermissionsServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<IRolesContext> _mockRolesContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IUnitsService> _mockUnitsService = new();
    private readonly Mock<IChainOfCommandService> _mockChainOfCommandService = new();
    private readonly Mock<IRecruitmentService> _mockRecruitmentService = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();

    private readonly PermissionsService _permissionsService;
    private readonly string _accountId = ObjectId.GenerateNewId().ToString();
    private readonly string _personnelUnitId = ObjectId.GenerateNewId().ToString();
    private readonly string _testersUnitId = ObjectId.GenerateNewId().ToString();
    private readonly string _missionsUnitId = ObjectId.GenerateNewId().ToString();

    public PermissionsServiceTests()
    {
        // Setup default variable values
        _mockVariablesService.Setup(x => x.GetVariable("PERMISSIONS_NCO_RANK")).Returns(new DomainVariableItem { Item = "Corporal" });
        _mockVariablesService.Setup(x => x.GetVariable("UNIT_ID_PERSONNEL")).Returns(new DomainVariableItem { Item = _personnelUnitId });
        _mockVariablesService.Setup(x => x.GetVariable("UNIT_ID_TESTERS")).Returns(new DomainVariableItem { Item = _testersUnitId });
        _mockVariablesService.Setup(x => x.GetVariable("UNIT_ID_MISSIONS")).Returns(new DomainVariableItem { Item = new[] { _missionsUnitId } });

        // Setup default unit responses
        _mockUnitsContext.Setup(x => x.GetSingle(_personnelUnitId)).Returns(new DomainUnit { Id = _personnelUnitId, Members = new List<string>() });
        _mockUnitsContext.Setup(x => x.GetSingle(_testersUnitId)).Returns(new DomainUnit { Id = _testersUnitId, Members = new List<string>() });
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<System.Func<DomainUnit, bool>>())).Returns(new List<DomainUnit>());

        _permissionsService = new PermissionsService(
            _mockAccountContext.Object,
            _mockRanksService.Object,
            _mockRolesContext.Object,
            _mockUnitsContext.Object,
            _mockUnitsService.Object,
            _mockChainOfCommandService.Object,
            _mockRecruitmentService.Object,
            _mockVariablesService.Object,
            _mockHttpContextService.Object
        );
    }

    #region Membership State Tests

    [Fact]
    public void GrantPermissions_Should_Return_Member_Permission_For_Member_State()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Member);
    }

    [Fact]
    public void GrantPermissions_Should_Return_Server_Permission_For_Server_State()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Server);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Admin);
        result.Should().NotContain(Permissions.Member);
    }

    [Fact]
    public void GrantPermissions_Should_Return_Confirmed_Permission_For_Confirmed_State()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Confirmed);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Confirmed);
        result.Should().NotContain(Permissions.Member);
    }

    [Fact]
    public void GrantPermissions_Should_Return_Discharged_Permission_For_Discharged_State()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Discharged);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Discharged);
        result.Should().NotContain(Permissions.Member);
    }

    [Fact]
    public void GrantPermissions_Should_Return_Unconfirmed_Permission_For_Unconfirmed_State()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Unconfirmed);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Unconfirmed);
        result.Should().NotContain(Permissions.Member);
    }

    [Fact]
    public void GrantPermissions_Should_Return_Unconfirmed_Permission_For_Empty_State()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Empty);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Unconfirmed);
        result.Should().NotContain(Permissions.Member);
    }

    #endregion

    #region Admin Permission Tests

    [Fact]
    public void GrantPermissions_Should_Return_All_Permissions_For_Admin_Member()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, admin: true);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Member);
        result.Should().Contain(Permissions.Admin);
        result.Should().Contain(Permissions.Command);
        result.Should().Contain(Permissions.Nco);
        result.Should().Contain(Permissions.Recruiter);
        result.Should().Contain(Permissions.RecruiterLead);
        result.Should().Contain(Permissions.Personnel);
        result.Should().Contain(Permissions.Servers);
        result.Should().Contain(Permissions.Tester);
        result.Should().NotContain(Permissions.Superadmin);
    }

    [Fact]
    public void GrantPermissions_Should_Return_Superadmin_Permission_For_SuperAdmin_Member()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, admin: true, superAdmin: true);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Superadmin);
        result.Should().Contain(Permissions.Admin);
        result.Should().Contain(Permissions.Member);
    }

    #endregion

    #region Command Permission Tests

    [Fact]
    public void GrantPermissions_Should_Grant_Command_Permission_For_Combat_Unit_Chain_Of_Command()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(account.Id)).Returns(true);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Command);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Command_Permission_For_Secondary_Unit_Chain_Of_Command()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(account.Id)).Returns(false);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Command);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Command_Permission_When_No_Chain_Of_Command_Position()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(account.Id)).Returns(false);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Command);
    }

    #endregion

    #region NCO Permission Tests

    [Fact]
    public void GrantPermissions_Should_Grant_Nco_Permission_For_NCO_Rank()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, rank: "Sergeant");
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Sergeant", "Corporal")).Returns(true);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Nco);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Nco_Permission_For_Lower_Rank()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, rank: "Private");
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Corporal")).Returns(false);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Nco);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Nco_Permission_When_Rank_Is_Null()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, rank: null);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Nco);
        _mockRanksService.Verify(x => x.IsSuperiorOrEqual(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Recruiter Permission Tests

    [Fact]
    public void GrantPermissions_Should_Grant_RecruiterLead_Permission_When_IsRecruiterLead()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        _mockRecruitmentService.Setup(x => x.IsRecruiterLead(account)).Returns(true);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.RecruiterLead);
    }

    [Fact]
    public void GrantPermissions_Should_Grant_Recruiter_Permission_When_IsRecruiter()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        _mockRecruitmentService.Setup(x => x.IsRecruiter(account)).Returns(true);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Recruiter);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Recruiter_Permissions_When_Not_Recruiter()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        _mockRecruitmentService.Setup(x => x.IsRecruiterLead(account)).Returns(false);
        _mockRecruitmentService.Setup(x => x.IsRecruiter(account)).Returns(false);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.RecruiterLead);
        result.Should().NotContain(Permissions.Recruiter);
    }

    #endregion

    #region Unit-Based Permission Tests

    [Fact]
    public void GrantPermissions_Should_Grant_Personnel_Permission_When_In_Personnel_Unit()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        var personnelUnit = new DomainUnit { Id = _personnelUnitId, Members = new List<string> { account.Id } };
        _mockUnitsContext.Setup(x => x.GetSingle(_personnelUnitId)).Returns(personnelUnit);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Personnel);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Personnel_Permission_When_Not_In_Personnel_Unit()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        var personnelUnit = new DomainUnit { Id = _personnelUnitId, Members = new List<string>() };
        _mockUnitsContext.Setup(x => x.GetSingle(_personnelUnitId)).Returns(personnelUnit);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Personnel);
    }

    [Fact]
    public void GrantPermissions_Should_Grant_Servers_Permission_When_In_Missions_Unit()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        var missionsUnit = new DomainUnit { Id = _missionsUnitId, Members = new List<string> { account.Id } };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<System.Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { missionsUnit });

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Servers);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Servers_Permission_When_Not_In_Missions_Unit()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        var missionsUnit = new DomainUnit { Id = _missionsUnitId, Members = new List<string>() };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<System.Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { missionsUnit });

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Servers);
    }

    [Fact]
    public void GrantPermissions_Should_Grant_Tester_Permission_When_In_Testers_Unit()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        var testersUnit = new DomainUnit { Id = _testersUnitId, Members = new List<string> { account.Id } };
        _mockUnitsContext.Setup(x => x.GetSingle(_testersUnitId)).Returns(testersUnit);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Tester);
    }

    [Fact]
    public void GrantPermissions_Should_Not_Grant_Tester_Permission_When_Not_In_Testers_Unit()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member);
        var testersUnit = new DomainUnit { Id = _testersUnitId, Members = new List<string>() };
        _mockUnitsContext.Setup(x => x.GetSingle(_testersUnitId)).Returns(testersUnit);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().NotContain(Permissions.Tester);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void GrantPermissions_Should_Grant_Multiple_Permissions_For_Complex_Member()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, rank: "Sergeant");
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Sergeant", "Corporal")).Returns(true);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(account.Id)).Returns(true);
        _mockRecruitmentService.Setup(x => x.IsRecruiter(account)).Returns(true);

        var personnelUnit = new DomainUnit { Id = _personnelUnitId, Members = new List<string> { account.Id } };
        _mockUnitsContext.Setup(x => x.GetSingle(_personnelUnitId)).Returns(personnelUnit);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().Contain(Permissions.Member);
        result.Should().Contain(Permissions.Command);
        result.Should().Contain(Permissions.Nco);
        result.Should().Contain(Permissions.Recruiter);
        result.Should().Contain(Permissions.Personnel);
        result.Should().NotContain(Permissions.Admin);
        result.Should().NotContain(Permissions.Superadmin);
    }

    [Fact]
    public void GrantPermissions_Should_Only_Return_Base_Permissions_For_Basic_Member()
    {
        // Arrange
        var account = CreateAccount(MembershipState.Member, rank: "Private");
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Corporal")).Returns(false);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(account.Id)).Returns(false);
        _mockRecruitmentService.Setup(x => x.IsRecruiterLead(account)).Returns(false);
        _mockRecruitmentService.Setup(x => x.IsRecruiter(account)).Returns(false);

        // Act
        var result = _permissionsService.GrantPermissions(account);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(Permissions.Member);
        result.Should().NotContain(Permissions.Command);
        result.Should().NotContain(Permissions.Nco);
        result.Should().NotContain(Permissions.Recruiter);
        result.Should().NotContain(Permissions.RecruiterLead);
        result.Should().NotContain(Permissions.Personnel);
        result.Should().NotContain(Permissions.Servers);
        result.Should().NotContain(Permissions.Tester);
    }

    #endregion

    #region Helper Methods

    private DomainAccount CreateAccount(MembershipState membershipState, bool admin = false, bool superAdmin = false, string rank = null)
    {
        return new DomainAccount
        {
            Id = _accountId,
            MembershipState = membershipState,
            Admin = admin,
            SuperAdmin = superAdmin,
            Rank = rank
        };
    }

    #endregion
}
