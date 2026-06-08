using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// <summary>
/// Drains the npcVoiceJobs queue: for each Pending mood, asks the clacks `emotion` role to
/// clone the base voice into that mood, stores the WAV as a {base}_{mood} variant, registers it,
/// and pushes it to the mesh. NodeDown (no emotion node up) leaves the mood Pending for the next
/// tick; a generation failure marks only that mood Failed. Offline/batch — polls every 30s.
/// </summary>
public class NpcMoodGenWorker(INpcVoiceJobsContext jobs, INpcVoicesContext voices, INpcVoiceStore store, IClacksClient clacks, IUksfLogger logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DrainOnceAsync();
            }
            catch (Exception e)
            {
                logger.LogError("NPC mood-gen drain failed", e);
            }
        }
    }

    public async Task DrainOnceAsync()
    {
        var pending = jobs.Get().Where(j => j.Moods.Any(m => m.Status is NpcMoodStatus.Pending)).ToList();
        foreach (var job in pending)
        {
            var baseDoc = voices.GetSingle(x => x.VoiceId == job.BaseVoiceId);
            if (baseDoc is null)
            {
                foreach (var m in job.Moods.Where(m => m.Status == NpcMoodStatus.Pending))
                {
                    m.Status = NpcMoodStatus.Failed;
                    m.Error = "base voice no longer exists";
                }

                await jobs.Replace(job);
                continue;
            }

            foreach (var task in job.Moods.Where(m => m.Status == NpcMoodStatus.Pending))
            {
                var script = MoodScripts.Table[task.Mood];
                var result = await clacks.EmoteAsync(job.BaseVoiceId, script.Script, script.EmoText, MoodScripts.EmoAlpha);
                if (result.Status == EmoteStatus.NodeDown)
                {
                    return; // no emotion node up — stop the whole drain, retry next tick
                }

                if (result.Status == EmoteStatus.Failed)
                {
                    task.Status = NpcMoodStatus.Failed;
                    task.Error = "generation failed";
                    await jobs.Replace(job);
                    continue;
                }

                await RegisterVariantAsync(baseDoc, task.Mood, result);
                task.Status = NpcMoodStatus.Ready;
                task.Error = null;
                await jobs.Replace(job);
            }
        }
    }

    private async Task RegisterVariantAsync(DomainNpcVoice baseDoc, string mood, ClacksEmoteResult result)
    {
        var voiceId = $"{baseDoc.VoiceId}_{mood}";
        var existing = voices.GetSingle(x => x.VoiceId == voiceId);
        if (existing is not null)
        {
            store.Delete(existing.FilePath);
            await voices.Delete(existing.Id);
        }

        var filePath = await store.SaveAsync(voiceId, result.WavBytes);
        var sha = Convert.ToHexString(SHA256.HashData(result.WavBytes)).ToLowerInvariant();
        await voices.Add(
            new DomainNpcVoice
            {
                VoiceId = voiceId,
                DisplayName = $"{baseDoc.DisplayName} ({mood})",
                OwnerId = baseDoc.OwnerId,
                MoodOf = baseDoc.VoiceId,
                FilePath = filePath,
                Sha256 = sha,
                DurationMs = result.DurationMs
            }
        );

        var pushed = await clacks.PutVoiceAsync(voiceId, result.WavBytes);
        if (!pushed)
        {
            logger.LogWarning($"Generated voice '{voiceId}' stored but clacks push failed — lazy-syncs on first use");
        }
    }
}
