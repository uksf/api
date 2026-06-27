using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class OpSessionCaptureServiceTests
{
    private readonly Mock<IOpsContext> _mockOps = new();
    private readonly OpSessionCaptureService _service;

    public OpSessionCaptureServiceTests()
    {
        _service = new OpSessionCaptureService(_mockOps.Object);
    }

    [Fact]
    public async Task CaptureStarted_stamps_most_recent_pending_op_for_server()
    {
        DomainOp older = new() { Id = "op1", LaunchedServerId = "s1", Status = OpStatus.Scheduled, LaunchedAt = new DateTime(2026, 6, 1) };
        DomainOp newer = new() { Id = "op2", LaunchedServerId = "s1", Status = OpStatus.Scheduled, LaunchedAt = new DateTime(2026, 6, 10) };
        _mockOps.Setup(x => x.Get(It.IsAny<Func<DomainOp, bool>>())).Returns([older, newer]);

        await _service.CaptureStartedAsync("s1", "sess-123");

        _mockOps.Verify(x => x.Update("op2", It.IsAny<Expression<Func<DomainOp, string>>>(), "sess-123"), Times.Once);
    }

    [Fact]
    public async Task CaptureStarted_noop_when_no_pending_op()
    {
        _mockOps.Setup(x => x.Get(It.IsAny<Func<DomainOp, bool>>())).Returns([]);

        await _service.CaptureStartedAsync("s1", "sess-123");

        _mockOps.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainOp, string>>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CaptureEnded_completes_matching_op()
    {
        DomainOp op = new() { Id = "op2", SessionId = "sess-123", Status = OpStatus.Scheduled };
        _mockOps.Setup(x => x.Get(It.IsAny<Func<DomainOp, bool>>())).Returns([op]);

        await _service.CaptureEndedAsync("sess-123");

        _mockOps.Verify(x => x.Update("op2", It.IsAny<Expression<Func<DomainOp, OpStatus>>>(), OpStatus.Complete), Times.Once);
    }
}
