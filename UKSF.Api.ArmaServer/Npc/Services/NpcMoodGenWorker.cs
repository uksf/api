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

            foreach (var mood in job.Moods.Where(m => m.Status == NpcMoodStatus.Pending).Select(m => m.Mood).ToList())
            {
                if (!StillQueued(job.Id, mood)) continue;

                var script = MoodScripts.Table[mood];
                var result = await clacks.EmoteAsync(job.BaseVoiceId, script.Script, script.EmoText, MoodScripts.EmoAlpha);
                if (result.Status == EmoteStatus.NodeDown)
                {
                    return;
                }

                if (!StillQueued(job.Id, mood) || voices.GetSingle(x => x.VoiceId == job.BaseVoiceId) is null) continue;

                if (result.Status == EmoteStatus.Failed)
                {
                    await SetStatusAsync(job.Id, mood, NpcMoodStatus.Failed, "generation failed");
                    continue;
                }

                await RegisterVariantAsync(baseDoc, mood, result);
                await SetStatusAsync(job.Id, mood, NpcMoodStatus.Ready, null);
            }

            await RenderMissingFillersAsync(baseDoc);
        }
    }

    private bool StillQueued(string jobId, string mood) => jobs.GetSingle(jobId)?.Moods.Any(m => m.Mood == mood && m.Status == NpcMoodStatus.Pending) == true;

    private async Task SetStatusAsync(string jobId, string mood, NpcMoodStatus status, string error)
    {
        var current = jobs.GetSingle(jobId);
        var task = current?.Moods.FirstOrDefault(m => m.Mood == mood);
        if (task is null) return;

        task.Status = status;
        task.Error = error;
        await jobs.Replace(current);
    }

    private async Task RenderMissingFillersAsync(DomainNpcVoice baseDoc)
    {
        var baseBytes = await store.ReadAsync(baseDoc.FilePath);
        var baseRms = baseBytes is null ? 0 : WavLoudness.Rms(baseBytes);
        foreach (var (fillerId, text) in NpcFillers.Table)
        {
            var path = NpcFillers.RelativePath(baseDoc.VoiceId, fillerId);
            if (store.Exists(path)) continue;

            var result = await clacks.EmoteAsync(baseDoc.VoiceId, text, NpcFillers.EmoText, NpcFillers.EmoAlpha);
            if (result is null || result.Status != EmoteStatus.Ok)
            {
                logger.LogWarning($"filler '{fillerId}' for voice '{baseDoc.VoiceId}' not rendered ({result?.Status.ToString() ?? "null"}) — retry next drain");
                return;
            }

            var wavBytes = baseRms > 0 ? WavLoudness.MatchRms(result.WavBytes, baseRms) : result.WavBytes;
            await store.SaveFillerAsync(baseDoc.VoiceId, NpcFillers.Slug(text), wavBytes);
            logger.LogInfo($"rendered filler '{text}' for voice '{baseDoc.VoiceId}'");
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

        var baseBytes = await store.ReadAsync(baseDoc.FilePath);
        var wavBytes = baseBytes is null ? result.WavBytes : WavLoudness.MatchRms(result.WavBytes, WavLoudness.Rms(baseBytes));

        var filePath = await store.SaveVariantAsync(baseDoc.VoiceId, mood, wavBytes);
        var sha = Convert.ToHexString(SHA256.HashData(wavBytes)).ToLowerInvariant();
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

        var pushed = await clacks.PutVoiceAsync(voiceId, wavBytes);
        if (!pushed)
        {
            logger.LogWarning($"Generated voice '{voiceId}' stored but clacks push failed — lazy-syncs on first use");
        }
    }
}
