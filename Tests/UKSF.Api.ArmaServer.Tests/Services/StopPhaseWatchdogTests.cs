using System;
using FluentAssertions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class StopPhaseWatchdogTests
{
    [Theory]
    [InlineData(StopPhase.None, 0, false)]
    [InlineData(StopPhase.Ending, 3600, false)]
    [InlineData(StopPhase.Saving, 3600, false)]
    [InlineData(StopPhase.Delivering, 134, false)]
    [InlineData(StopPhase.Delivering, 136, true)]
    [InlineData(StopPhase.Stopping, 29, false)]
    [InlineData(StopPhase.Stopping, 31, true)]
    public void WatchdogExceeded_ArmedUsesPerStageCeiling(StopPhase phase, int secondsInPhase, bool expected)
    {
        var now = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = phase,
            StopPhaseEnteredAt = phase == StopPhase.None ? null : now.AddSeconds(-secondsInPhase),
            StopRequestedAt = now.AddSeconds(-secondsInPhase)
        };

        StopPhaseWatchdog.WatchdogExceeded(status, now).Should().Be(expected);
    }

    [Theory]
    [InlineData(179, false)]
    [InlineData(181, true)]
    public void WatchdogExceeded_WithoutGameEvents_UsesBackstopFromStopRequest(int secondsSinceRequested, bool expected)
    {
        var now = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Ending,
            StopPhaseEnteredAt = null,
            StopRequestedAt = now.AddSeconds(-secondsSinceRequested)
        };

        StopPhaseWatchdog.WatchdogExceeded(status, now).Should().Be(expected);
    }

    [Fact]
    public void WatchdogExceeded_ArmedPastBackstopButWithinPerStage_NotExceeded()
    {
        var now = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Delivering,
            StopPhaseEnteredAt = now.AddSeconds(-130),
            StopRequestedAt = now.AddSeconds(-300)
        };

        StopPhaseWatchdog.WatchdogExceeded(status, now).Should().BeFalse();
    }

    [Fact]
    public void KillOfferAt_WhenNotStopping_IsNull()
    {
        StopPhaseWatchdog.KillOfferAt(new GameServerStatus { StopPhase = StopPhase.None }).Should().BeNull();
    }

    [Theory]
    [InlineData(StopPhase.Ending, 10)]
    [InlineData(StopPhase.Saving, 45)]
    [InlineData(StopPhase.Delivering, 0)]
    [InlineData(StopPhase.Stopping, 20)]
    public void KillOfferAt_ArmedUsesPerStageOffer(StopPhase phase, int expectedOfferSeconds)
    {
        var entered = DateTime.UtcNow;
        var status = new GameServerStatus { StopPhase = phase, StopPhaseEnteredAt = entered };

        StopPhaseWatchdog.KillOfferAt(status).Should().Be(entered.AddSeconds(expectedOfferSeconds));
    }

    [Fact]
    public void KillOfferAt_WithoutGameEvents_UsesBackstopOffer()
    {
        var requested = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Ending,
            StopPhaseEnteredAt = null,
            StopRequestedAt = requested
        };

        StopPhaseWatchdog.KillOfferAt(status).Should().Be(requested.AddSeconds(60));
    }

    [Fact]
    public void KillOfferAt_ALaterPhaseNeverWithdrawsAnOfferAlreadyMade()
    {
        var now = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Stopping,
            StopPhaseEnteredAt = now,
            KillAllowedAt = now.AddSeconds(-5)
        };

        StopPhaseWatchdog.KillOfferAt(status).Should().Be(now.AddSeconds(-5));
    }

    [Theory]
    [InlineData(StopPhase.Delivering, 135)]
    [InlineData(StopPhase.Stopping, 30)]
    public void KillOfferAt_LandsBeforeTheApiForceKills(StopPhase phase, int ceilingSeconds)
    {
        var entered = DateTime.UtcNow;
        var status = new GameServerStatus { StopPhase = phase, StopPhaseEnteredAt = entered };

        StopPhaseWatchdog.KillOfferAt(status).Should().BeBefore(entered.AddSeconds(ceilingSeconds));
    }

    [Fact]
    public void Rank_PutsDeliveringBetweenSavingAndStopping()
    {
        StopPhaseWatchdog.Rank(StopPhase.Saving).Should().BeLessThan(StopPhaseWatchdog.Rank(StopPhase.Delivering));
        StopPhaseWatchdog.Rank(StopPhase.Delivering).Should().BeLessThan(StopPhaseWatchdog.Rank(StopPhase.Stopping));
    }

    [Fact]
    public void StopPhase_KeepsTheNumbersStoredInMongoAndReadByTheWeb()
    {
        new[] { (int)StopPhase.None, (int)StopPhase.Ending, (int)StopPhase.Saving, (int)StopPhase.Stopping, (int)StopPhase.Delivering }.Should()
            .Equal(0, 1, 2, 3, 4);
    }
}
