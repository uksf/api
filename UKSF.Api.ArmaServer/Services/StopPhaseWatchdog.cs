using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public static class StopPhaseWatchdog
{
    private static readonly TimeSpan SqfPlayerDrainCap = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ExtensionSaveDeliveryWindow = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan LegacyFlushThenShutdownDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MeasuredProcessTeardown = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan PhaseEventMargin = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan EndingCeiling = SqfPlayerDrainCap + TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DeliveringCeiling = ExtensionSaveDeliveryWindow + PhaseEventMargin;
    private static readonly TimeSpan StoppingCeiling = LegacyFlushThenShutdownDelay + MeasuredProcessTeardown + TimeSpan.FromSeconds(11);
    private static readonly TimeSpan BackstopCeiling = TimeSpan.FromSeconds(180);

    private static readonly TimeSpan EndingKillOffer = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SavingKillOffer = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan DeliveringKillOffer = TimeSpan.Zero;
    private static readonly TimeSpan StoppingKillOffer = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BackstopKillOffer = TimeSpan.FromSeconds(60);

    public static int Rank(StopPhase phase) =>
        phase switch
        {
            StopPhase.None       => 0,
            StopPhase.Ending     => 1,
            StopPhase.Saving     => 2,
            StopPhase.Delivering => 3,
            StopPhase.Stopping   => 4,
            _                    => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };

    public static bool WatchdogExceeded(GameServerStatus status, DateTime nowUtc)
    {
        if (status.StopPhase == StopPhase.None)
        {
            return false;
        }

        if (status.StopPhaseEnteredAt is { } enteredAt)
        {
            TimeSpan? ceiling = status.StopPhase switch
            {
                StopPhase.Ending     => EndingCeiling,
                StopPhase.Saving     => null,
                StopPhase.Delivering => DeliveringCeiling,
                StopPhase.Stopping   => StoppingCeiling,
                _                    => BackstopCeiling
            };
            return ceiling is { } limit && nowUtc - enteredAt > limit;
        }

        return status.StopRequestedAt is { } requestedAt && nowUtc - requestedAt > BackstopCeiling;
    }

    public static DateTime? KillOfferAt(GameServerStatus status)
    {
        if (status.StopPhase == StopPhase.None)
        {
            return null;
        }

        DateTime? offer = status.StopPhaseEnteredAt is { } enteredAt ? enteredAt +
                                                                       status.StopPhase switch
                                                                       {
                                                                           StopPhase.Ending     => EndingKillOffer,
                                                                           StopPhase.Saving     => SavingKillOffer,
                                                                           StopPhase.Delivering => DeliveringKillOffer,
                                                                           StopPhase.Stopping   => StoppingKillOffer,
                                                                           _                    => BackstopKillOffer
                                                                       } :
            status.StopRequestedAt is { } requestedAt ? requestedAt + BackstopKillOffer : null;

        if (offer is null)
        {
            return status.KillAllowedAt;
        }

        return status.KillAllowedAt is { } existing && existing < offer ? existing : offer;
    }
}
