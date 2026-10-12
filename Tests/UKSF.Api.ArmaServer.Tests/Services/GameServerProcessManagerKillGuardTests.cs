using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerKillGuardTests : GameServerProcessManagerTestBase
{
    private static DomainGameServer Server(StopPhase phase, bool running = true) =>
        new()
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 999999,
            Status = new GameServerStatus { StopPhase = phase, Running = running }
        };

    public GameServerProcessManagerKillGuardTests()
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
    }

    [Fact]
    public async Task KillServer_WhenThePhaseMovedToSavingAfterTheControllerCheck_RefusesWithoutConfirmation()
    {
        var stale = Server(StopPhase.None);
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(Server(StopPhase.Saving));

        var act = () => _sut.KillServerAsync(stale);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*persistence data*");
        _mockProcessUtilities.Verify(x => x.FindProcessById(It.IsAny<int>()), Times.Never);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task KillServer_WhileSavingWithConfirmation_ResetsTheServer()
    {
        var current = Server(StopPhase.Saving);
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(current);

        await _sut.KillServerAsync(Server(StopPhase.None), confirmDataLoss: true);

        current.ProcessId.Should().BeNull();
        current.Status.StopPhase.Should().Be(StopPhase.None);
        _mockContext.Verify(x => x.Replace(current), Times.Once);
    }

    [Fact]
    public async Task KillServer_WhenThePidWasReused_DoesNotKillTheStranger()
    {
        var process = Process.GetCurrentProcess();
        var server = Server(StopPhase.None);
        server.ProcessId = process.Id;
        server.ProcessStartedAt = process.StartTime.ToUniversalTime().AddMinutes(-10);
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(server);
        _mockProcessUtilities.Setup(x => x.FindProcessById(process.Id)).Returns(process);
        _mockHelpers.Setup(x => x.IsOwnedProcessId(It.IsAny<int>())).Returns(false);

        await _sut.KillServerAsync(server);

        _mockHelpers.Verify(x => x.IsOwnedProcessId(It.IsAny<int>()), Times.Never);
        server.ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task KillServer_AfterVerifyingTheProcess_ChecksOwnershipOfThatProcessOnce()
    {
        var process = Process.GetCurrentProcess();
        var server = Server(StopPhase.None);
        server.ProcessId = process.Id;
        server.ProcessStartedAt = process.StartTime.ToUniversalTime();
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(server);
        _mockProcessUtilities.Setup(x => x.FindProcessById(process.Id)).Returns(process);
        _mockHelpers.Setup(x => x.IsOwnedProcessId(process.Id)).Returns(false);

        await _sut.KillServerAsync(server);

        _mockHelpers.Verify(x => x.IsOwnedProcessId(process.Id), Times.Once);
        server.ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task KillAll_WhenAServerWasAddedWhileWaitingForLocks_RefusesAndKillsNothing()
    {
        _mockContext.SetupSequence(x => x.Get())
                    .Returns(new List<DomainGameServer> { Server(StopPhase.None) })
                    .Returns(
                        new List<DomainGameServer>
                        {
                            Server(StopPhase.None),
                            new()
                            {
                                Id = "s2",
                                Name = "New",
                                Status = new GameServerStatus { StopPhase = StopPhase.Saving }
                            }
                        }
                    );

        var act = () => _sut.KillAllAsync(confirmDataLoss: false);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*server list changed*");
        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Never);
    }

    [Fact]
    public async Task KillAll_WhenAServerIsSavingUnderTheLock_RefusesWithoutConfirmationAndKillsNothing()
    {
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { Server(StopPhase.Delivering) });

        var act = () => _sut.KillAllAsync();

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Test still saving*");
        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Never);
    }

    [Fact]
    public async Task KillAll_WhenAServerIsSavingWithConfirmation_KillsAndResets()
    {
        var server = Server(StopPhase.Saving);
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(server);

        await _sut.KillAllAsync(confirmDataLoss: true);

        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Once);
        server.ProcessId.Should().BeNull();
        server.Status.StopPhase.Should().Be(StopPhase.None);
    }

    [Fact]
    public async Task Stop_WhenTheServerIsAlreadySavingAndNotRunning_DoesNotKillIt()
    {
        var server = Server(StopPhase.Saving, running: false);

        await _sut.StopServerAsync(server);

        server.ProcessId.Should().Be(999999);
        server.Status.StopPhase.Should().Be(StopPhase.Saving);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(m => m.Contains("already stopping"))), Times.Once);
    }

    [Fact]
    public async Task Stop_WhenTheStoredServerMovedToSavingAfterTheControllerRead_DoesNotKillIt()
    {
        var current = Server(StopPhase.Saving, running: false);
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(current);

        await _sut.StopServerAsync(Server(StopPhase.None, running: false));

        current.ProcessId.Should().Be(999999);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(m => m.Contains("already stopping"))), Times.Once);
    }
}
