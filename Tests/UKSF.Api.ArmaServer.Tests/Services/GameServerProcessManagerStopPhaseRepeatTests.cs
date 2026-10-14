using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerStopPhaseRepeatTests : GameServerProcessManagerTestBase
{
    [Theory]
    [InlineData(StopPhase.Ending)]
    [InlineData(StopPhase.Saving)]
    [InlineData(StopPhase.Delivering)]
    [InlineData(StopPhase.Stopping)]
    public async Task A_retried_phase_event_keeps_the_time_the_phase_was_entered(StopPhase phase)
    {
        var enteredAt = DateTime.UtcNow.AddSeconds(-100);
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus
            {
                StopPhase = phase,
                StopRequestedAt = enteredAt,
                StopPhaseEnteredAt = enteredAt
            }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockContext.Setup(x => x.GetSingle("s1")).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await (phase switch
        {
            StopPhase.Ending     => _sut.HandleStopEndingAsync(2303),
            StopPhase.Saving     => _sut.HandleStopSavingAsync(2303),
            StopPhase.Delivering => _sut.HandleStopDeliveringAsync(2303),
            _                    => _sut.HandleStopStoppingAsync(2303)
        });

        server.Status.StopPhase.Should().Be(phase);
        server.Status.StopPhaseEnteredAt.Should().Be(enteredAt);
    }
}
