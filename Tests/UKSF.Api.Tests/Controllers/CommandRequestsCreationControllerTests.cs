using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class CommandRequestsCreationControllerTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<ICommandRequestService> _mockCommandRequestService;
    private readonly Mock<IRanksService> _mockRanksService;
    private readonly Mock<IChainOfCommandService> _mockChainOfCommandService;
    private readonly Mock<IHttpContextService> _mockHttpContextService;
    private readonly CommandRequestsCreationController _controller;

    private readonly string _requesterId = ObjectId.GenerateNewId().ToString();
    private readonly string _recipientId = ObjectId.GenerateNewId().ToString();
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();

    public CommandRequestsCreationControllerTests()
    {
        _mockAccountContext = new Mock<IAccountContext>();
        _mockUnitsContext = new Mock<IUnitsContext>();
        _mockCommandRequestService = new Mock<ICommandRequestService>();
        _mockRanksService = new Mock<IRanksService>();
        var mockLoaService = new Mock<ILoaService>();
        _mockChainOfCommandService = new Mock<IChainOfCommandService>();
        var mockDisplayNameService = new Mock<IDisplayNameService>();
        _mockHttpContextService = new Mock<IHttpContextService>();

        _controller = new CommandRequestsCreationController(
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockCommandRequestService.Object,
            _mockRanksService.Object,
            mockLoaService.Object,
            _mockChainOfCommandService.Object,
            mockDisplayNameService.Object,
            _mockHttpContextService.Object
        );

        SetupCommonMocks();
    }

    [Fact]
    public async Task CreateRequestTransfer_Should_Create_Combat_Transfer_Request()
    {
        // Arrange
        var request = CreateTransferRequest();
        var combatUnit = CreateUnit(_unitId, "Combat Unit", UnitBranch.Combat);
        var recipient = new DomainAccount { Id = _recipientId, UnitAssignment = "Current Unit" };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(combatUnit);
        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestTransfer(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.Transfer &&
                                                 r.DisplayValue == combatUnit.Name &&
                                                 r.DisplayFrom == recipient.UnitAssignment
                ),
                ChainOfCommandMode.Commander_And_Target_Commander
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestTransfer_Should_Create_Auxiliary_Transfer_Request()
    {
        // Arrange
        var request = CreateTransferRequest();
        var auxiliaryUnit = CreateUnit(_unitId, "Auxiliary Unit", UnitBranch.Auxiliary);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(auxiliaryUnit);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestTransfer(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.AuxiliaryTransfer &&
                                                 r.DisplayValue == auxiliaryUnit.Name &&
                                                 r.DisplayFrom == "N/A"
                ),
                ChainOfCommandMode.Target_Commander
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestTransfer_Should_Create_Secondary_Transfer_Request()
    {
        // Arrange
        var request = CreateTransferRequest();
        var secondaryUnit = CreateUnit(_unitId, "Secondary Unit", UnitBranch.Secondary);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(secondaryUnit);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestTransfer(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.SecondaryTransfer &&
                                                 r.DisplayValue == secondaryUnit.Name &&
                                                 r.DisplayFrom == "N/A"
                ),
                ChainOfCommandMode.Target_Commander
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestTransfer_Should_Throw_When_Equivalent_Request_Exists()
    {
        // Arrange
        var request = CreateTransferRequest();
        var combatUnit = CreateUnit(_unitId, "Combat Unit", UnitBranch.Combat);
        var recipient = new DomainAccount { Id = _recipientId, UnitAssignment = "Current Unit" };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(combatUnit);
        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _controller.CreateRequestTransfer(request));
        exception.Message.Should().Be("An equivalent request already exists");
    }

    [Fact]
    public async Task CreateRequestUnitRemoval_Should_Create_Unit_Removal_Request()
    {
        // Arrange
        var request = CreateUnitRemovalRequest();
        var auxiliaryUnit = CreateUnit(_unitId, "Auxiliary Unit", UnitBranch.Auxiliary);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(auxiliaryUnit);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestUnitRemoval(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.UnitRemoval && r.DisplayValue == "N/A" && r.DisplayFrom == auxiliaryUnit.Name),
                ChainOfCommandMode.Target_Commander
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestUnitRemoval_Should_Throw_When_Combat_Unit()
    {
        // Arrange
        var request = CreateUnitRemovalRequest();
        var combatUnit = CreateUnit(_unitId, "Combat Unit", UnitBranch.Combat);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(combatUnit);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _controller.CreateRequestUnitRemoval(request));
        exception.Message.Should().Be("To remove from a combat unit, use a Transfer request");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Create_Unit_Role_Request()
    {
        // Arrange
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient] // Add recipient to members
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(true);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestChainOfCommandPosition(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.ChainOfCommandPosition &&
                                                 r.Recipient == request.Recipient &&
                                                 r.Value == request.Value &&
                                                 r.SecondaryValue == request.SecondaryValue
                ),
                It.IsAny<ChainOfCommandMode>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Throw_When_No_Role_And_Removing()
    {
        // Arrange
        var request = CreateUnitRoleRequest("None");
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient] // Add recipient to members
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(false);

        // Act & Assert
        var action = async () => await _controller.CreateRequestChainOfCommandPosition(request);
        var exception = await action.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should()
                 .Be(" has no chain of command position in Test Unit. If you are trying to remove them from the unit, use a Unit Removal request");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Throw_When_Member_Not_In_Unit()
    {
        // Arrange
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand()
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);

        // Act & Assert
        var action = async () => await _controller.CreateRequestChainOfCommandPosition(request);
        var exception = await action.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should().Be(" is not a member of Test Unit. They must be a unit member before being assigned to a chain of command position");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Throw_When_Assigning_To_Current_Position()
    {
        // Arrange
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient] // Add recipient to members
        };
        unit.ChainOfCommand.First = request.Recipient;

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(true);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPosition(request.Recipient, It.IsAny<DomainUnit>(), "1iC")).Returns(true);

        // Act & Assert
        var action = async () => await _controller.CreateRequestChainOfCommandPosition(request);
        var exception = await action.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should().Be(" is already assigned to 1iC in Test Unit");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Allow_Reassigning_To_Different_Position()
    {
        // Arrange
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient] // Add recipient to members
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(true);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPosition(request.Recipient, It.IsAny<DomainUnit>(), request.SecondaryValue))
                                  .Returns(false);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestChainOfCommandPosition(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.ChainOfCommandPosition &&
                                                 r.Recipient == request.Recipient &&
                                                 r.Value == request.Value &&
                                                 r.SecondaryValue == request.SecondaryValue
                ),
                It.IsAny<ChainOfCommandMode>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestRank_Should_Create_Promotion_Request()
    {
        // Arrange
        var request = CreateRankRequest();
        var recipient = new DomainAccount { Id = _recipientId, Rank = "Private" };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockRanksService.Setup(x => x.IsSuperior(request.Value, recipient.Rank)).Returns(true);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestRank(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.Promotion && r.DisplayValue == request.Value && r.DisplayFrom == recipient.Rank),
                It.IsAny<ChainOfCommandMode>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestRank_Should_Create_Demotion_Request()
    {
        // Arrange
        var request = CreateRankRequest();
        var recipient = new DomainAccount { Id = _recipientId, Rank = "Sergeant" };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockRanksService.Setup(x => x.IsSuperior(request.Value, recipient.Rank)).Returns(false);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestRank(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.Demotion && r.DisplayValue == request.Value && r.DisplayFrom == recipient.Rank),
                It.IsAny<ChainOfCommandMode>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestRank_Should_Throw_When_Ranks_Are_Equal()
    {
        // Arrange
        var request = CreateRankRequest();
        var recipient = new DomainAccount { Id = _recipientId, Rank = request.Value };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _controller.CreateRequestRank(request));
        exception.Message.Should().Be("Ranks are equal");
    }

    [Fact]
    public async Task CreateRequestDischarge_Should_Create_Discharge_Request()
    {
        // Arrange
        var request = CreateDischargeRequest();
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestDischarge(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.Discharge && r.DisplayValue == "Discharged" && r.DisplayFrom == "Member"),
                ChainOfCommandMode.Commander_And_Personnel
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestIndividualRole_Should_Create_Individual_Role_Request()
    {
        // Arrange
        var request = CreateIndividualRoleRequest();
        var recipient = new DomainAccount { Id = _recipientId, RoleAssignment = "Current Role" };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestIndividualRole(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.Role &&
                                                 r.Recipient == request.Recipient &&
                                                 r.Value == request.Value &&
                                                 r.DisplayFrom == recipient.RoleAssignment
                ),
                ChainOfCommandMode.Next_Commander
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRequestReinstateMember_Should_Create_Reinstate_Request()
    {
        // Arrange
        var request = CreateReinstateRequest();
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        // Act
        await _controller.CreateRequestReinstateMember(request);

        // Assert
        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.ReinstateMember && r.DisplayValue == "Member" && r.DisplayFrom == "Discharged"),
                ChainOfCommandMode.Personnel
            ),
            Times.Once
        );
    }

    private void SetupCommonMocks()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_requesterId);
    }

    private DomainCommandRequest CreateTransferRequest()
    {
        return new DomainCommandRequest
        {
            Recipient = _recipientId,
            Value = _unitId,
            Reason = "Test transfer"
        };
    }

    private DomainCommandRequest CreateUnitRemovalRequest()
    {
        return new DomainCommandRequest
        {
            Recipient = _recipientId,
            Value = _unitId,
            Reason = "Test removal"
        };
    }

    private DomainCommandRequest CreateUnitRoleRequest(string position = "1iC")
    {
        return new DomainCommandRequest
        {
            Recipient = _recipientId,
            Value = _unitId,
            SecondaryValue = position,
            Reason = "Test role assignment"
        };
    }

    private DomainCommandRequest CreateRankRequest()
    {
        return new DomainCommandRequest
        {
            Recipient = _recipientId,
            Value = "Corporal",
            Reason = "Test rank change"
        };
    }

    private DomainCommandRequest CreateDischargeRequest()
    {
        return new DomainCommandRequest { Recipient = _recipientId, Reason = "Test discharge" };
    }

    private DomainCommandRequest CreateIndividualRoleRequest()
    {
        return new DomainCommandRequest
        {
            Recipient = _recipientId,
            Value = "New Role",
            Reason = "Test role change"
        };
    }

    private DomainCommandRequest CreateReinstateRequest()
    {
        return new DomainCommandRequest { Recipient = _recipientId, Reason = "Test reinstatement" };
    }

    private DomainUnit CreateUnit(string id, string name, UnitBranch branch)
    {
        return new DomainUnit
        {
            Id = id,
            Name = name,
            Branch = branch,
            Members = []
        };
    }
}
