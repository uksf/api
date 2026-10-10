using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using System.Diagnostics;
using FluentAssertions;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerIdentityTests : GameServerProcessManagerTestBase
{
    private readonly Process _process = Process.GetCurrentProcess();

    [Fact]
    public void IsServerProcess_WithTheRecordedStartTime_IsTrue()
    {
        var server = new DomainGameServer { ProcessId = _process.Id, ProcessStartedAt = _process.StartTime.ToUniversalTime() };

        _sut.IsServerProcess(server, _process).Should().BeTrue();
    }

    [Fact]
    public void IsServerProcess_WhenThePidWasReusedByALaterProcess_IsFalse()
    {
        var server = new DomainGameServer { ProcessId = _process.Id, ProcessStartedAt = _process.StartTime.ToUniversalTime().AddMinutes(-10) };

        _sut.IsServerProcess(server, _process).Should().BeFalse();
    }

    [Fact]
    public void IsServerProcess_ForARecordWithoutAStartTime_TrustsThePid()
    {
        var server = new DomainGameServer { ProcessId = _process.Id };

        _sut.IsServerProcess(server, _process).Should().BeTrue();
    }

    [Fact]
    public void StartTimeOf_ReturnsTheProcessStartInUtc()
    {
        _sut.StartTimeOf(_process).Should().Be(_process.StartTime.ToUniversalTime());
    }

    [Fact]
    public async Task Monitor_WhenAStopCeilingPassedButThePidWasReused_ResetsTheServerWithoutKillingTheStranger()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = _process.Id,
            ProcessStartedAt = _process.StartTime.ToUniversalTime().AddMinutes(-10),
            Status = new GameServerStatus { StopPhase = StopPhase.Delivering, StopPhaseEnteredAt = DateTime.UtcNow.AddMinutes(-10) }
        };
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
