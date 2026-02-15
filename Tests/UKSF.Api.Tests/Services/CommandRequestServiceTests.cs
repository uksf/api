using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class CommandRequestServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<ICommandRequestContext> _mockCommandRequestContext = new();
    private readonly Mock<ICommandRequestArchiveContext> _mockArchiveContext = new();
    private readonly Mock<INotificationsService> _mockNotificationsService = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<IAccountService> _mockAccountService = new();
    private readonly Mock<IChainOfCommandService> _mockChainOfCommandService = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly CommandRequestService _subject;

    public CommandRequestServiceTests()
    {
        _subject = new CommandRequestService(
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockCommandRequestContext.Object,
            _mockArchiveContext.Object,
            _mockNotificationsService.Object,
            _mockDisplayNameService.Object,
            _mockAccountService.Object,
            _mockChainOfCommandService.Object,
            _mockRanksService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Add_ShouldCreateRequestWithReviewers()
    {
        var requester = new DomainAccount
        {
            Id = "requester1",
            Firstname = "Tim",
            Lastname = "Smith"
        };
        var recipient = new DomainAccount
        {
            Id = "recipient1",
            Firstname = "John",
            Lastname = "Doe",
            UnitAssignment = "1 Section"
        };
        var reviewer = new DomainAccount
        {
            Id = "reviewer1",
            Firstname = "Bob",
            Lastname = "Jones",
            Rank = "Sgt"
        };
        var unit = new DomainUnit { Name = "1 Section" };
        var request = new DomainCommandRequest
        {
            Recipient = "recipient1",
            Type = "Transfer",
            Value = "target-unit",
            Reason = "Test"
        };

        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(requester);
        _mockAccountContext.Setup(x => x.GetSingle("recipient1")).Returns(recipient);
        _mockAccountContext.Setup(x => x.GetSingle("reviewer1")).Returns(reviewer);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(requester)).Returns("Tim Smith");
        _mockDisplayNameService.Setup(x => x.GetDisplayName(recipient)).Returns("John Doe");
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);
        _mockUnitsContext.Setup(x => x.GetSingle("target-unit")).Returns(new DomainUnit { Name = "Target" });
        _mockChainOfCommandService.Setup(x => x.ResolveChain(It.IsAny<ChainOfCommandMode>(), "recipient1", unit, It.IsAny<DomainUnit>()))
                                  .Returns(new HashSet<string> { "reviewer1" });
        _mockRanksService.Setup(x => x.Sort(It.IsAny<string>(), It.IsAny<string>())).Returns(0);

        await _subject.Add(request);

        request.Reviews.Should().ContainKey("reviewer1");
        request.Reviews["reviewer1"].Should().Be(ReviewState.Pending);
        request.DisplayRequester.Should().Be("Tim Smith");
        request.DisplayRecipient.Should().Be("John Doe");
        _mockCommandRequestContext.Verify(x => x.Add(request), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(It.IsAny<DomainNotification>()), Times.Once);
    }

    [Fact]
    public async Task Add_ShouldThrow_WhenNoReviewersResolved()
    {
        var requester = new DomainAccount { Id = "requester1" };
        var recipient = new DomainAccount { Id = "recipient1", UnitAssignment = "1 Section" };
        var request = new DomainCommandRequest
        {
            Recipient = "recipient1",
            Type = "Transfer",
            Value = "target-unit"
        };

        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(requester);
        _mockAccountContext.Setup(x => x.GetSingle("recipient1")).Returns(recipient);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Name");
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(new DomainUnit());
        _mockUnitsContext.Setup(x => x.GetSingle("target-unit")).Returns(new DomainUnit());
        _mockChainOfCommandService
            .Setup(x => x.ResolveChain(It.IsAny<ChainOfCommandMode>(), It.IsAny<string>(), It.IsAny<DomainUnit>(), It.IsAny<DomainUnit>()))
            .Returns(new HashSet<string>());

        var act = () => _subject.Add(request);

        await act.Should().ThrowAsync<Exception>().WithMessage("*Failed to get any commanders*");
    }

    [Fact]
    public async Task Add_ShouldNotNotifyRequester_WhenRequesterIsReviewer()
    {
        var requester = new DomainAccount
        {
            Id = "user1",
            Firstname = "Tim",
            Lastname = "Smith",
            Rank = "Sgt"
        };
        var request = new DomainCommandRequest
        {
            Recipient = "user1",
            Type = "LOA",
            Value = "",
            Reason = "Holiday"
        };

        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(requester);
        _mockAccountContext.Setup(x => x.GetSingle("user1")).Returns(requester);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(requester)).Returns("Tim Smith");
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(new DomainUnit());
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(new DomainUnit());
        _mockChainOfCommandService
            .Setup(x => x.ResolveChain(It.IsAny<ChainOfCommandMode>(), It.IsAny<string>(), It.IsAny<DomainUnit>(), It.IsAny<DomainUnit>()))
            .Returns(new HashSet<string> { "user1" });
        _mockRanksService.Setup(x => x.Sort(It.IsAny<string>(), It.IsAny<string>())).Returns(0);

        await _subject.Add(request);

        _mockNotificationsService.Verify(x => x.Add(It.IsAny<DomainNotification>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveRequest_ShouldMoveRequestToArchive()
    {
        var request = new DomainCommandRequest { Id = "req1" };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        await _subject.ArchiveRequest("req1");

        _mockArchiveContext.Verify(x => x.Add(request), Times.Once);
        _mockCommandRequestContext.Verify(x => x.Delete("req1"), Times.Once);
    }

    [Fact]
    public async Task SetRequestReviewState_ShouldUpdateReviewerState()
    {
        var request = new DomainCommandRequest { Id = "req1", Reviews = { { "reviewer1", ReviewState.Pending } } };

        await _subject.SetRequestReviewState(request, "reviewer1", ReviewState.Approved);

        _mockCommandRequestContext.Verify(x => x.Update("req1", It.IsAny<UpdateDefinition<DomainCommandRequest>>()), Times.Once);
    }

    [Fact]
    public async Task SetRequestAllReviewStates_ShouldUpdateAllReviewers()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1", Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Pending }, { "r2", ReviewState.Pending } }
        };

        await _subject.SetRequestAllReviewStates(request, ReviewState.Approved);

        _mockCommandRequestContext.Verify(x => x.Update("req1", It.IsAny<UpdateDefinition<DomainCommandRequest>>()), Times.Once);
    }

    [Fact]
    public async Task SetRequestAllReviewStates_ShouldNotMutateCachedRequest()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1", Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Pending }, { "r2", ReviewState.Pending } }
        };

        await _subject.SetRequestAllReviewStates(request, ReviewState.Approved);

        request.Reviews["r1"].Should().Be(ReviewState.Pending);
        request.Reviews["r2"].Should().Be(ReviewState.Pending);
    }

    [Fact]
    public void GetReviewState_ShouldReturnReviewerState()
    {
        var request = new DomainCommandRequest { Id = "req1", Reviews = { { "reviewer1", ReviewState.Approved } } };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        var result = _subject.GetReviewState("req1", "reviewer1");

        result.Should().Be(ReviewState.Approved);
    }

    [Fact]
    public void GetReviewState_ShouldReturnError_WhenRequestNotFound()
    {
        _mockCommandRequestContext.Setup(x => x.GetSingle("missing")).Returns((DomainCommandRequest)null);

        var result = _subject.GetReviewState("missing", "reviewer1");

        result.Should().Be(ReviewState.Error);
    }

    [Fact]
    public void GetReviewState_ShouldReturnError_WhenReviewerNotInRequest()
    {
        var request = new DomainCommandRequest { Id = "req1", Reviews = { { "reviewer1", ReviewState.Pending } } };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        var result = _subject.GetReviewState("req1", "unknown");

        result.Should().Be(ReviewState.Error);
    }

    [Fact]
    public void IsRequestApproved_ShouldReturnTrue_WhenAllReviewersApproved()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1", Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Approved }, { "r2", ReviewState.Approved } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        _subject.IsRequestApproved("req1").Should().BeTrue();
    }

    [Fact]
    public void IsRequestApproved_ShouldReturnFalse_WhenAnyReviewerPending()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1", Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Approved }, { "r2", ReviewState.Pending } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        _subject.IsRequestApproved("req1").Should().BeFalse();
    }

    [Fact]
    public void IsRequestRejected_ShouldReturnTrue_WhenAnyReviewerRejected()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1", Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Approved }, { "r2", ReviewState.Rejected } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        _subject.IsRequestRejected("req1").Should().BeTrue();
    }

    [Fact]
    public void IsRequestRejected_ShouldReturnFalse_WhenNoReviewerRejected()
    {
        var request = new DomainCommandRequest
        {
            Id = "req1", Reviews = new Dictionary<string, ReviewState> { { "r1", ReviewState.Approved }, { "r2", ReviewState.Pending } }
        };
        _mockCommandRequestContext.Setup(x => x.GetSingle("req1")).Returns(request);

        _subject.IsRequestRejected("req1").Should().BeFalse();
    }

    [Fact]
    public void DoesEquivalentRequestExist_ShouldReturnTrue_WhenMatchingRequestExists()
    {
        var existing = new DomainCommandRequest
        {
            Recipient = "user1",
            Type = "Transfer",
            DisplayValue = "2 Section",
            DisplayFrom = "1 Section"
        };
        _mockCommandRequestContext.Setup(x => x.Get()).Returns(new List<DomainCommandRequest> { existing });

        var request = new DomainCommandRequest
        {
            Recipient = "user1",
            Type = "Transfer",
            DisplayValue = "2 Section",
            DisplayFrom = "1 Section"
        };

        _subject.DoesEquivalentRequestExist(request).Should().BeTrue();
    }

    [Fact]
    public void DoesEquivalentRequestExist_ShouldReturnFalse_WhenNoMatchingRequest()
    {
        _mockCommandRequestContext.Setup(x => x.Get()).Returns(new List<DomainCommandRequest>());

        var request = new DomainCommandRequest
        {
            Recipient = "user1",
            Type = "Transfer",
            DisplayValue = "2 Section"
        };

        _subject.DoesEquivalentRequestExist(request).Should().BeFalse();
    }
}
