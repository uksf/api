using System;
using FluentAssertions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class StopPhaseWatchdogTests
{
    [Theory]
    [InlineData(StopPhase.None, 0, false)] // not stopping
    [InlineData(StopPhase.Ending, 14, false)] // armed, within 15s
    [InlineData(StopPhase.Ending, 16, true)] // armed, past 15s
    [InlineData(StopPhase.Saving, 119, false)] // armed, within 120s
    [InlineData(StopPhase.Saving, 121, true)] // armed, past 120s
    [InlineData(StopPhase.Stopping, 59, false)] // armed, within 60s
    [InlineData(StopPhase.Stopping, 61, true)] // armed, past 60s
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
    [InlineData(179, false)] // unarmed backstop, within 180s
    [InlineData(181, true)] // unarmed backstop, past 180s
    public void WatchdogExceeded_UnarmedUsesBackstop(int secondsSinceRequested, bool expected)
    {
        var now = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Ending, // API-set provisional phase
            StopPhaseEnteredAt = null, // NOT armed (old modpack: no game event)
            StopRequestedAt = now.AddSeconds(-secondsSinceRequested)
        };

        StopPhaseWatchdog.WatchdogExceeded(status, now).Should().Be(expected);
    }

    [Fact]
    public void WatchdogExceeded_ArmedPastBackstopButWithinPerStage_NotExceeded()
    {
        // Mutual exclusivity: an armed server long past 180s-from-request but within its
        // per-stage ceiling is NOT killed (backstop applies only when unarmed).
        var now = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Saving,
            StopPhaseEnteredAt = now.AddSeconds(-100), // within 120s Saving ceiling
            StopRequestedAt = now.AddSeconds(-300) // way past 180s backstop
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
    [InlineData(StopPhase.Stopping, 20)]
    public void KillOfferAt_ArmedUsesPerStageOffer(StopPhase phase, int expectedOfferSeconds)
    {
        var entered = DateTime.UtcNow;
        var status = new GameServerStatus { StopPhase = phase, StopPhaseEnteredAt = entered };

        StopPhaseWatchdog.KillOfferAt(status).Should().Be(entered.AddSeconds(expectedOfferSeconds));
    }

    [Fact]
    public void KillOfferAt_UnarmedUsesBackstopOffer()
    {
        var requested = DateTime.UtcNow;
        var status = new GameServerStatus
        {
            StopPhase = StopPhase.Ending,
            StopPhaseEnteredAt = null, // old modpack: no game events arrive
            StopRequestedAt = requested
        };

        StopPhaseWatchdog.KillOfferAt(status).Should().Be(requested.AddSeconds(60));
    }

    [Fact]
    public void KillOfferAt_KeepsEarlierExistingOffer()
    {
        // A later phase must never withdraw a kill the web has already offered.
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
    [InlineData(StopPhase.Ending, 15)]
    [InlineData(StopPhase.Saving, 120)]
    [InlineData(StopPhase.Stopping, 60)]
    public void KillOfferAt_AlwaysPrecedesForceKillCeiling(StopPhase phase, int ceilingSeconds)
    {
        // The offer must land before the watchdog fires, or the button appears only as the
        // API force-kills the server anyway.
        var entered = DateTime.UtcNow;
        var status = new GameServerStatus { StopPhase = phase, StopPhaseEnteredAt = entered };

        StopPhaseWatchdog.KillOfferAt(status).Should().BeBefore(entered.AddSeconds(ceilingSeconds));
    }
}
