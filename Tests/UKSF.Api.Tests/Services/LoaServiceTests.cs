using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class LoaServiceTests
{
    private readonly ILoaService _loaService;
    private readonly Mock<ILoaContext> _mockLoaDataService;

    public LoaServiceTests()
    {
        _mockLoaDataService = new Mock<ILoaContext>();

        _loaService = new LoaService(_mockLoaDataService.Object);
    }

    [Fact]
    public void ShouldGetCorrectLoas()
    {
        DomainLoa loa1 = new() { Recipient = "5ed524b04f5b532a5437bba1", End = DateTime.UtcNow.AddDays(-5) };
        DomainLoa loa2 = new() { Recipient = "5ed524b04f5b532a5437bba1", End = DateTime.UtcNow.AddDays(-35) };
        DomainLoa loa3 = new() { Recipient = "5ed524b04f5b532a5437bba2", End = DateTime.UtcNow.AddDays(-45) };
        DomainLoa loa4 = new() { Recipient = "5ed524b04f5b532a5437bba2", End = DateTime.UtcNow.AddDays(-30).AddSeconds(1) };
        DomainLoa loa5 = new() { Recipient = "5ed524b04f5b532a5437bba3", End = DateTime.UtcNow.AddDays(-5) };
        List<DomainLoa> mockCollection = [loa1, loa2, loa3, loa4, loa5];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(x => mockCollection.Where(x).ToList());

        var subject = _loaService.Get(["5ed524b04f5b532a5437bba1", "5ed524b04f5b532a5437bba2"]);

        subject.Should().Contain(new List<DomainLoa> { loa1, loa4 });
    }

    [Fact]
    public async Task Add_creates_loa_with_correct_properties()
    {
        var start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var request = new CreateLoaRequest
        {
            Reason = "Holiday",
            Start = start,
            End = end,
            Emergency = true,
            Late = true
        };
        List<DomainLoa> mockCollection = [];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        await _loaService.Add(request, "recipient1", "Going on holiday");

        _mockLoaDataService.Verify(
            x => x.Add(
                It.Is<DomainLoa>(loa => loa.Recipient == "recipient1" &&
                                        loa.Start == start &&
                                        loa.End == end &&
                                        loa.Reason == "Going on holiday" &&
                                        loa.Emergency == true &&
                                        loa.Late == true
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Add_returns_loa_id()
    {
        var request = new CreateLoaRequest
        {
            Reason = "Holiday",
            Start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        List<DomainLoa> mockCollection = [];
        DomainLoa capturedLoa = null;

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());
        _mockLoaDataService.Setup(x => x.Add(It.IsAny<DomainLoa>())).Callback<DomainLoa>(loa => capturedLoa = loa);

        var result = await _loaService.Add(request, "recipient1", "reason");

        result.Should().Be(capturedLoa.Id);
    }

    [Fact]
    public async Task Add_throws_when_overlapping_non_rejected_loa_exists()
    {
        var start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var request = new CreateLoaRequest
        {
            Reason = "Holiday",
            Start = start.AddDays(1),
            End = end.AddDays(-1)
        };
        List<DomainLoa> mockCollection =
        [
            new()
            {
                Recipient = "recipient1",
                Start = start,
                End = end,
                State = LoaReviewState.Pending
            }
        ];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        var act = () => _loaService.Add(request, "recipient1", "reason");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("An LOA covering the same date range already exists");
    }

    [Fact]
    public async Task Add_allows_when_overlapping_loa_is_rejected()
    {
        var start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var request = new CreateLoaRequest
        {
            Reason = "Holiday",
            Start = start.AddDays(1),
            End = end.AddDays(-1)
        };
        List<DomainLoa> mockCollection =
        [
            new()
            {
                Recipient = "recipient1",
                Start = start,
                End = end,
                State = LoaReviewState.Rejected
            }
        ];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        var act = () => _loaService.Add(request, "recipient1", "reason");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Add_allows_when_no_overlap()
    {
        var request = new CreateLoaRequest
        {
            Reason = "Holiday",
            Start = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2025, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        List<DomainLoa> mockCollection =
        [
            new()
            {
                Recipient = "recipient1",
                Start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                State = LoaReviewState.Pending
            }
        ];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        var act = () => _loaService.Add(request, "recipient1", "reason");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetLoaState_calls_update_with_correct_id()
    {
        await _loaService.SetLoaState("loa-id-123", LoaReviewState.Approved);

        _mockLoaDataService.Verify(x => x.Update("loa-id-123", It.IsAny<UpdateDefinition<DomainLoa>>()), Times.Once);
    }

    [Fact]
    public async Task SetLoaState_updates_state()
    {
        await _loaService.SetLoaState("loa-id-123", LoaReviewState.Rejected);

        _mockLoaDataService.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainLoa>>()), Times.Once);
    }

    [Fact]
    public void IsLoaCovered_returns_true_when_event_within_loa_range()
    {
        var eventStart = new DateTime(2025, 3, 5, 12, 0, 0, DateTimeKind.Utc);
        List<DomainLoa> mockCollection =
        [
            new()
            {
                Recipient = "recipient1",
                Start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc)
            }
        ];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        var result = _loaService.IsLoaCovered("recipient1", eventStart);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsLoaCovered_returns_false_when_event_outside_loa_range()
    {
        var eventStart = new DateTime(2025, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        List<DomainLoa> mockCollection =
        [
            new()
            {
                Recipient = "recipient1",
                Start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc)
            }
        ];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        var result = _loaService.IsLoaCovered("recipient1", eventStart);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsLoaCovered_returns_false_when_no_loa_for_recipient()
    {
        var eventStart = new DateTime(2025, 3, 5, 12, 0, 0, DateTimeKind.Utc);
        List<DomainLoa> mockCollection =
        [
            new()
            {
                Recipient = "other-recipient",
                Start = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc)
            }
        ];

        _mockLoaDataService.Setup(x => x.Get(It.IsAny<Func<DomainLoa, bool>>())).Returns<Func<DomainLoa, bool>>(pred => mockCollection.Where(pred).ToList());

        var result = _loaService.IsLoaCovered("recipient1", eventStart);

        result.Should().BeFalse();
    }
}
