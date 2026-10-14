using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using System.Diagnostics;
using FluentAssertions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerIdentityTests : GameServerProcessManagerTestBase
{
    private readonly Process _process = Process.GetCurrentProcess();

    [Fact]
    public void TryOpenOwnedProcess_WithTheRecordedStartTime_ReturnsTheProcess()
    {
        _mockProcessUtilities.Setup(x => x.FindProcessById(_process.Id)).Returns(_process);

        _sut.TryOpenOwnedProcess(new TrackedProcess(_process.Id, GameServerProcessManager.CanonicalStartTime(_process.StartTime))).Should().BeSameAs(_process);
    }

    [Fact]
    public void TryOpenOwnedProcess_WhenThePidWasReusedByALaterProcess_ReturnsNull()
    {
        _mockProcessUtilities.Setup(x => x.FindProcessById(_process.Id)).Returns(_process);

        _sut.TryOpenOwnedProcess(new TrackedProcess(_process.Id, GameServerProcessManager.CanonicalStartTime(_process.StartTime).AddMinutes(-10)))
            .Should()
            .BeNull();
    }

    [Fact]
    public void TryOpenOwnedProcess_WhenAnotherApiOwnsThePid_ReturnsNullWithoutOpeningIt()
    {
        _mockHelpers.Setup(x => x.IsOwnedProcessId(_process.Id)).Returns(false);

        _sut.TryOpenOwnedProcess(new TrackedProcess(_process.Id, GameServerProcessManager.CanonicalStartTime(_process.StartTime))).Should().BeNull();
        _mockProcessUtilities.Verify(x => x.FindProcessById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void StartTimeOf_ReturnsTheProcessStartInUtcAtMongoPrecision()
    {
        var expected = _process.StartTime.ToUniversalTime();
        expected = expected.AddTicks(-(expected.Ticks % TimeSpan.TicksPerMillisecond));

        _sut.StartTimeOf(_process).Should().Be(expected);
        _sut.StartTimeOf(_process)!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Monitor_WhenAStopCeilingPassedButThePidWasReused_ResetsTheServerWithoutKillingTheStranger()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = _process.Id,
            ProcessStartedAt = GameServerProcessManager.CanonicalStartTime(_process.StartTime).AddMinutes(-10),
            Status = new GameServerStatus { StopPhase = StopPhase.Delivering, StopPhaseEnteredAt = DateTime.UtcNow.AddMinutes(-10) }
        };
        _servers.Add(server);
        _mockProcessUtilities.Setup(x => x.FindProcessById(_process.Id)).Returns(_process);
        var calls = 0;
        _mockContext.Setup(x => x.Get()).Returns(() => ++calls == 1 ? new List<DomainGameServer> { server } : new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.EnsureMonitorRunning();
        await Task.Delay(1000);

        server.ProcessId.Should().BeNull();
        server.ProcessStartedAt.Should().BeNull();
        _process.HasExited.Should().BeFalse();
    }
}
