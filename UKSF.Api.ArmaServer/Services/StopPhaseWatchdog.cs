using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

/// <summary>
///     Stop-phase timing. Each phase has two limits: the point a stop is slower than the
///     shutdown normally takes, after which the web may offer a manual kill, and the ceiling
///     at which the API force-kills the process itself.
/// </summary>
public static class StopPhaseWatchdog
{
    private static readonly TimeSpan EndingCeiling = TimeSpan.FromSeconds(15); // 10s SQF drain cap + 5s buffer so shutdown_saving lands before force-kill
    private static readonly TimeSpan SavingCeiling = TimeSpan.FromSeconds(120); // == SQF object-save cap
    private static readonly TimeSpan StoppingCeiling = TimeSpan.FromSeconds(60); // 5s SQF pre-#shutdown + engine config teardown (often 10-30s+ with CBA/ACE)
    private static readonly TimeSpan BackstopCeiling = TimeSpan.FromSeconds(180); // old modpack (30s drain): full ~155s shutdown + margin

    private static readonly TimeSpan EndingKillOffer = TimeSpan.FromSeconds(10); // player drain is 10s at worst
    private static readonly TimeSpan SavingKillOffer = TimeSpan.FromSeconds(45); // a healthy save of a full mission
    private static readonly TimeSpan StoppingKillOffer = TimeSpan.FromSeconds(20); // 5s SQF wait + a slow but normal engine teardown
    private static readonly TimeSpan BackstopKillOffer = TimeSpan.FromSeconds(60); // no game events: whole shutdown judged as one span

    public static bool WatchdogExceeded(GameServerStatus status, DateTime nowUtc)
    {
        if (status.StopPhase == StopPhase.None)
        {
            return false;
        }

        if (status.StopPhaseEnteredAt is { } enteredAt)
        {
            var ceiling = status.StopPhase switch
            {
                StopPhase.Ending   => EndingCeiling,
                StopPhase.Saving   => SavingCeiling,
                StopPhase.Stopping => StoppingCeiling,
                _                  => BackstopCeiling
            };
            return nowUtc - enteredAt > ceiling;
        }

        return status.StopRequestedAt is { } requestedAt && nowUtc - requestedAt > BackstopCeiling;
    }

    /// <summary>
    ///     The instant the web may offer a manual kill for the stop in progress. Never moves
    ///     later than an offer already made, so a phase change cannot withdraw the button.
    /// </summary>
    public static DateTime? KillOfferAt(GameServerStatus status)
    {
        if (status.StopPhase == StopPhase.None)
        {
            return null;
        }

        DateTime? offer = status.StopPhaseEnteredAt is { } enteredAt ? enteredAt +
                                                                       status.StopPhase switch
                                                                       {
                                                                           StopPhase.Ending   => EndingKillOffer,
                                                                           StopPhase.Saving   => SavingKillOffer,
                                                                           StopPhase.Stopping => StoppingKillOffer,
                                                                           _                  => BackstopKillOffer
                                                                       } :
            status.StopRequestedAt is { } requestedAt ? requestedAt + BackstopKillOffer : null;

        if (offer is null)
        {
            return status.KillAllowedAt;
        }

        return status.KillAllowedAt is { } existing && existing < offer ? existing : offer;
    }
}
