using System;
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
}
