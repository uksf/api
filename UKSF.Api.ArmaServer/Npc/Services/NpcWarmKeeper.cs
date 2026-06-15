using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// <summary>
/// Keeps the NPC chat + voice engines warm on the clacks mesh while any NPC session is live,
/// so mid-mission turns never pay a cold model load. Renews a short lease each tick (lease >
/// tick, so it never lapses between renewals); when the mission ends and sessions are cleared,
/// renewals stop and the engines idle-unload. mood-gen stays the tolerant batch worker (not warmed).
/// </summary>
public class NpcWarmKeeper(INpcSessionsContext sessions, IClacksClient clacks, IUksfLogger logger) : BackgroundService
{
    public static readonly IReadOnlyCollection<string> WarmRoles = ["npc", "npc-voice"];
    public const int LeaseMs = 180_000;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(120);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync();
            }
            catch (Exception e)
            {
                logger.LogError("NPC warm-keeper tick failed", e);
            }
        }
    }

    public async Task TickAsync()
    {
        if (!sessions.Get().Any()) return; // no live NPCs — let the engines idle-unload
        await clacks.WarmAsync(WarmRoles, LeaseMs);
    }
}
