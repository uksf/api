using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class CommandRequestsControllerTests
{
    private readonly Mock<ICommandRequestService> _mockCommandRequestService = new();
    private readonly Mock<ICommandRequestCompletionService> _mockCommandRequestCompletionService = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<ICommandRequestContext> _mockCommandRequestContext = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<INotificationsService> _mockNotificationsService = new();
    private readonly Mock<IVariablesContext> _mockVariablesContext = new();
    private readonly Mock<IAccountService> _mockAccountService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<ILoaService> _mockLoaService = new();
    private readonly Mock<IChainOfCommandService> _mockChainOfCommandService = new();
    private readonly CommandRequestsController _subject;

    private readonly string _requesterId = ObjectId.GenerateNewId().ToString();
    private readonly string _recipientId = ObjectId.GenerateNewId().ToString();
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();

    public CommandRequestsControllerTests()
    {
        _subject = new CommandRequestsController(
            _mockCommandRequestService.Object,
            _mockCommandRequestCompletionService.Object,
            _mockHttpContextService.Object,
            _mockUnitsContext.Object,
            _mockCommandRequestContext.Object,
            _mockDisplayNameService.Object,
            _mockNotificationsService.Object,
            _mockVariablesContext.Object,
            _mockAccountService.Object,
            _mockLogger.Object,
            _mockAccountContext.Object,
            _mockRanksService.Object,
            _mockLoaService.Object,
            _mockChainOfCommandService.Object
        );

        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_requesterId);
    }

    [Fact]
    public async Task UpdateRequestReview_WhenOverridden_CallsSetRequestOverride_NotAllReviewStates()
    {
        var existingReviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Approved }, { "r2", ReviewState.Pending } };
        var request = new DomainCommandRequest
        {
            Id = "req1",
            Type = CommandRequestType.Loa,
            DisplayRecipient = "Cpl Bridg",
            Reviews = existingReviews
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);
        var actorAccount = new DomainAccount { Id = "actor1" };
        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(actorAccount);

        await _subject.UpdateRequestReview("req1", new UpdateCommandReviewRequest { ReviewState = ReviewState.Approved, Overriden = true });

        _mockCommandRequestService.Verify(x => x.SetRequestOverride(request, ReviewState.Approved, "actor1"), Times.Once);
        request.Reviews.Should().BeEquivalentTo(existingReviews);
    }

    [Fact]
    public async Task UpdateRequestReview_WhenOverrideResolveFails_RollsBackViaClearRequestOverride()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1",
            Type = CommandRequestType.Loa,
            DisplayRecipient = "Cpl Bridg",
            Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Pending } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);
        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Id = "actor1" });
        _mockCommandRequestCompletionService.Setup(x => x.Resolve("req1")).ThrowsAsync(new Exception("boom"));

        var act = async () => await _subject.UpdateRequestReview(
            "req1",
            new UpdateCommandReviewRequest { ReviewState = ReviewState.Approved, Overriden = true }
        );
        await Assert.ThrowsAsync<BadRequestException>(act);

        _mockCommandRequestService.Verify(x => x.ClearRequestOverride(request), Times.Once);
        _mockCommandRequestService.Verify(x => x.SetRequestOverride(It.IsAny<DomainCommandRequest>(), ReviewState.Pending, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRequestReview_WhenVoteResolves_DoesNotEmitIntermediateReviewStateAudit()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1",
            Type = CommandRequestType.Loa,
            DisplayRecipient = "Cpl Bridg",
            Reviews = new Dictionary<string, ReviewState> { { "actor1", ReviewState.Pending } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);
        _mockCommandRequestService.Setup(x => x.GetReviewState("req1", "actor1")).Returns(ReviewState.Pending);
        _mockCommandRequestService.Setup(x => x.IsRequestApproved(It.IsAny<DomainCommandRequest>())).Returns(true);
        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Id = "actor1" });

        await _subject.UpdateRequestReview("req1", new UpdateCommandReviewRequest { ReviewState = ReviewState.Approved, Overriden = false });

        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Review state of") && s.Contains("updated to")), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRequestReview_WhenVoteDoesNotResolve_StillEmitsIntermediateAudit()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1",
            Type = CommandRequestType.Loa,
            DisplayRecipient = "Cpl Bridg",
            Reviews = new Dictionary<string, ReviewState> { { "actor1", ReviewState.Pending }, { "other", ReviewState.Pending } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);
        _mockCommandRequestService.Setup(x => x.GetReviewState("req1", "actor1")).Returns(ReviewState.Pending);
        _mockCommandRequestService.Setup(x => x.IsRequestApproved(It.IsAny<DomainCommandRequest>())).Returns(false);
        _mockCommandRequestService.Setup(x => x.IsRequestRejected(It.IsAny<DomainCommandRequest>())).Returns(false);
        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(new DomainAccount { Id = "actor1" });
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Tim");

        await _subject.UpdateRequestReview("req1", new UpdateCommandReviewRequest { ReviewState = ReviewState.Approved, Overriden = false });

        _mockLogger.Verify(
            x => x.LogAudit(It.Is<string>(s => s.Contains("Review state of") && s.Contains("Tim") && s.Contains("updated to")), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Get_PopulatesIconKeyAndColorKey_PerType()
    {
        var promotion = new DomainCommandRequest
        {
            Id = "p1",
            Type = CommandRequestType.Promotion,
            Reviews = new Dictionary<string, ReviewState> { { "actor", ReviewState.Pending } }
        };
        var loa = new DomainCommandRequest
        {
            Id = "l1",
            Type = CommandRequestType.Loa,
            Reviews = new Dictionary<string, ReviewState> { { "actor", ReviewState.Pending } }
        };
        _mockCommandRequestContext.Setup(x => x.Get()).Returns(new[] { promotion, loa });
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("actor");
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(new DomainUnit { Members = new List<string> { "actor" } });
        _mockVariablesContext.Setup(x => x.GetSingle("UNIT_ID_PERSONNEL")).Returns(new DomainVariableItem { Item = "unit1" });

        var result = _subject.Get();

        var p = result.MyRequests.First(x => x.Data.Type == CommandRequestType.Promotion);
        p.IconKey.Should().Be(Icons.Promotion);
        p.ColorKey.Should().Be("promotion");

        var l = result.MyRequests.First(x => x.Data.Type == CommandRequestType.Loa);
        l.IconKey.Should().Be(Icons.Loa);
        l.ColorKey.Should().Be("loa");
    }

    [Fact]
    public async Task CreateRequestTransfer_Should_Create_Combat_Transfer_Request()
    {
        var request = CreateTransferRequest();
        var combatUnit = CreateUnit(_unitId, "Combat Unit", UnitBranch.Combat);
        var recipient = new DomainAccount { Id = _recipientId, UnitAssignment = "Current Unit" };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(combatUnit);
        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestTransfer(request);

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
        var request = CreateTransferRequest();
        var auxiliaryUnit = CreateUnit(_unitId, "Auxiliary Unit", UnitBranch.Auxiliary);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(auxiliaryUnit);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestTransfer(request);

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
        var request = CreateTransferRequest();
        var secondaryUnit = CreateUnit(_unitId, "Secondary Unit", UnitBranch.Secondary);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(secondaryUnit);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestTransfer(request);

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
        var request = CreateTransferRequest();
        var combatUnit = CreateUnit(_unitId, "Combat Unit", UnitBranch.Combat);
        var recipient = new DomainAccount { Id = _recipientId, UnitAssignment = "Current Unit" };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(combatUnit);
        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _subject.CreateRequestTransfer(request));
        exception.Message.Should().Be("An equivalent request already exists");
    }

    [Fact]
    public async Task CreateRequestUnitRemoval_Should_Create_Unit_Removal_Request()
    {
        var request = CreateUnitRemovalRequest();
        var auxiliaryUnit = CreateUnit(_unitId, "Auxiliary Unit", UnitBranch.Auxiliary);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(auxiliaryUnit);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestUnitRemoval(request);

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
        var request = CreateUnitRemovalRequest();
        var combatUnit = CreateUnit(_unitId, "Combat Unit", UnitBranch.Combat);

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(combatUnit);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _subject.CreateRequestUnitRemoval(request));
        exception.Message.Should().Be("To remove from a combat unit, use a Transfer request");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Create_Unit_Role_Request()
    {
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient]
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(true);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestChainOfCommandPosition(request);

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
        var request = CreateUnitRoleRequest("None");
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient]
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(false);

        var action = async () => await _subject.CreateRequestChainOfCommandPosition(request);
        var exception = await action.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should()
                 .Be(" has no chain of command position in Test Unit. If you are trying to remove them from the unit, use a Unit Removal request");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Throw_When_Member_Not_In_Unit()
    {
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand()
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);

        var action = async () => await _subject.CreateRequestChainOfCommandPosition(request);
        var exception = await action.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should().Be(" is not a member of Test Unit. They must be a unit member before being assigned to a chain of command position");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Throw_When_Assigning_To_Current_Position()
    {
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient]
        };
        unit.ChainOfCommand.First = request.Recipient;

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(true);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPosition(request.Recipient, It.IsAny<DomainUnit>(), "1iC")).Returns(true);

        var action = async () => await _subject.CreateRequestChainOfCommandPosition(request);
        var exception = await action.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should().Be(" is already assigned to 1iC in Test Unit");
    }

    [Fact]
    public async Task CreateRequestChainOfCommandPosition_Should_Allow_Reassigning_To_Different_Position()
    {
        var request = CreateUnitRoleRequest();
        var unit = new DomainUnit
        {
            Id = request.Value,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand(),
            Members = [request.Recipient]
        };

        _mockUnitsContext.Setup(x => x.GetSingle(request.Value)).Returns(unit);
        _mockChainOfCommandService.Setup(x => x.ChainOfCommandHasMember(It.IsAny<DomainUnit>(), request.Recipient)).Returns(true);
        _mockChainOfCommandService.Setup(x => x.MemberHasChainOfCommandPosition(request.Recipient, It.IsAny<DomainUnit>(), request.SecondaryValue))
                                  .Returns(false);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestChainOfCommandPosition(request);

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
        var request = CreateRankRequest();
        var recipient = new DomainAccount { Id = _recipientId, Rank = "Private" };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockRanksService.Setup(x => x.IsSuperior(request.Value, recipient.Rank)).Returns(true);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestRank(request);

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
        var request = CreateRankRequest();
        var recipient = new DomainAccount { Id = _recipientId, Rank = "Sergeant" };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockRanksService.Setup(x => x.IsSuperior(request.Value, recipient.Rank)).Returns(false);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestRank(request);

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
        var request = CreateRankRequest();
        var recipient = new DomainAccount { Id = _recipientId, Rank = request.Value };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _subject.CreateRequestRank(request));
        exception.Message.Should().Be("Ranks are equal");
    }

    [Fact]
    public async Task CreateRequestDischarge_Should_Create_Discharge_Request()
    {
        var request = CreateDischargeRequest();
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestDischarge(request);

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
        var request = CreateIndividualRoleRequest();
        var recipient = new DomainAccount { Id = _recipientId, RoleAssignment = "Current Role" };

        _mockAccountContext.Setup(x => x.GetSingle(request.Recipient)).Returns(recipient);
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestIndividualRole(request);

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
        var request = CreateReinstateRequest();
        _mockCommandRequestService.Setup(x => x.DoesEquivalentRequestExist(It.IsAny<DomainCommandRequest>())).Returns(false);

        await _subject.CreateRequestReinstateMember(request);

        _mockCommandRequestService.Verify(
            x => x.Add(
                It.Is<DomainCommandRequest>(r => r.Type == CommandRequestType.ReinstateMember && r.DisplayValue == "Member" && r.DisplayFrom == "Discharged"),
                ChainOfCommandMode.Personnel
            ),
            Times.Once
        );
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
