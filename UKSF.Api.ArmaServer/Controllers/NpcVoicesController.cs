using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Nco, Permissions.Servers, Permissions.Command, Permissions.Admin)]
public class NpcVoicesController(
    INpcVoicesContext context,
    INpcVoiceJobsContext jobs,
    INpcVoiceStore store,
    IClacksClient clacks,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : ControllerBase
{
    private const int MaxBytes = 5242880; // 5 MB — also enforced by RequestSizeLimit before model binding
    private const long MinMs = 3000;
    private const long MaxMs = 15000;

    [HttpGet]
    [Authorize]
    public List<DomainNpcVoice> GetVoices()
    {
        return context.Get().OrderBy(x => x.VoiceId).ToList();
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(MaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
    public async Task<DomainNpcVoice> Upload([FromForm] IFormFile sample, [FromForm] string displayName, [FromForm] string moodOf, [FromForm] string moodLabel)
    {
        if (sample is null || sample.Length == 0)
        {
            throw new BadRequestException("No sample file provided");
        }

        using var ms = new MemoryStream();
        await sample.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var wav = WavValidator.Parse(bytes);
        if (!wav.Valid)
        {
            throw new BadRequestException($"Invalid WAV: {wav.Error}");
        }

        if (wav.DurationMs < MinMs || wav.DurationMs > MaxMs)
        {
            throw new BadRequestException($"Sample must be 3-15 s (got {wav.DurationMs / 1000.0:0.0} s)");
        }

        var parentVoiceId = string.IsNullOrWhiteSpace(moodOf) ? null : VoiceSlug.Slugify(moodOf);
        DomainNpcVoice parent = null;
        if (parentVoiceId is not null)
        {
            parent = context.GetSingle(x => x.VoiceId == parentVoiceId) ?? throw new BadRequestException($"Parent voice '{parentVoiceId}' not found");
            if (!MoodScripts.IsValid(VoiceSlug.Slugify(moodLabel)))
            {
                throw new BadRequestException($"Mood must be one of {string.Join(", ", MoodScripts.Generated)}");
            }

            if (!CanManage(parent))
            {
                throw new BadRequestException("Only the owner or an admin can add moods to this voice");
            }
        }

        string voiceId;
        try
        {
            voiceId = VoiceSlug.Derive(displayName, moodOf, moodLabel);
        }
        catch (ArgumentException exception)
        {
            throw new BadRequestException(exception.Message);
        }

        var existing = context.GetSingle(x => x.VoiceId == voiceId);
        if (existing is not null && parent is null)
        {
            throw new BadRequestException($"Voice '{voiceId}' already exists");
        }

        if (existing is not null)
        {
            // A recorded mood replaces the current one, generated or not.
            store.Delete(existing.FilePath);
            await context.Delete(existing.Id);
        }

        var filePath = await store.SaveBaseAsync(voiceId, bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var doc = new DomainNpcVoice
        {
            VoiceId = voiceId,
            DisplayName = parent is null ? displayName : $"{parent.DisplayName} ({VoiceSlug.Slugify(moodLabel)})",
            OwnerId = httpContextService.GetUserId(),
            MoodOf = parentVoiceId,
            FilePath = filePath,
            Sha256 = sha,
            DurationMs = wav.DurationMs
        };
        await context.Add(doc);
        if (parent is not null)
        {
            await DropFromJobAsync(parent.VoiceId, VoiceSlug.Slugify(moodLabel));
        }

        var pushed = await clacks.PutVoiceAsync(voiceId, bytes);
        if (!pushed)
        {
            logger.LogWarning($"Voice '{voiceId}' stored but clacks push failed — it will lazy-sync on first use");
        }

        logger.LogAudit($"Uploaded NPC voice '{voiceId}'");
        return doc;
    }

    [HttpGet("{id}/sample")]
    [Authorize]
    public async Task<IActionResult> GetSample(string id)
    {
        var doc = context.GetSingle(id);
        if (doc is null)
        {
            return NotFound();
        }

        var bytes = await store.ReadAsync(doc.FilePath);
        if (bytes is null)
        {
            return NotFound();
        }

        return File(bytes, "audio/wav", $"{doc.VoiceId}.wav");
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id)
    {
        var doc = context.GetSingle(id);
        if (doc is null)
        {
            return NotFound();
        }

        if (!CanManage(doc))
        {
            return Forbid();
        }

        if (doc.MoodOf is null)
        {
            // A mood variant cannot outlive its base: the page lists moods under their base only.
            foreach (var variant in context.Get(x => x.MoodOf == doc.VoiceId).ToList())
            {
                store.Delete(variant.FilePath);
                await context.Delete(variant.Id);
            }

            var job = jobs.GetSingle(x => x.BaseVoiceId == doc.VoiceId);
            if (job is not null)
            {
                await jobs.Delete(job.Id);
            }
        }
        else
        {
            await DropFromJobAsync(doc.MoodOf, doc.VoiceId[(doc.MoodOf.Length + 1)..]);
        }

        store.Delete(doc.FilePath);
        await context.Delete(id);
        logger.LogAudit($"Deleted NPC voice '{doc.VoiceId}'");
        return Ok();
    }

    /// Queues the requested moods, or every mood the voice does not have yet. Moods already queued
    /// for this voice stay queued.
    [HttpPost("{baseVoiceId}/generate-moods")]
    [Authorize]
    public async Task<DomainNpcVoiceJob> GenerateMoods(string baseVoiceId, [FromBody] GenerateMoodsRequest request = null)
    {
        var baseDoc = context.GetSingle(x => x.VoiceId == baseVoiceId);
        if (baseDoc is null)
        {
            throw new BadRequestException($"Voice '{baseVoiceId}' not found");
        }

        if (baseDoc.MoodOf is not null)
        {
            throw new BadRequestException("Moods can only be generated for a base voice, not a mood variant");
        }

        if (!CanManage(baseDoc))
        {
            throw new BadRequestException("Only the owner or an admin can generate moods for this voice");
        }

        var requested = request?.Moods?.Select(VoiceSlug.Slugify).Distinct().ToList() ?? [];
        var unknown = requested.FirstOrDefault(m => !MoodScripts.IsValid(m));
        if (unknown is not null)
        {
            throw new BadRequestException($"Unknown mood '{unknown}'");
        }

        if (requested.Count == 0)
        {
            var have = context.Get(x => x.MoodOf == baseVoiceId).Select(x => x.VoiceId).ToHashSet();
            requested = MoodScripts.Generated.Where(m => !have.Contains($"{baseVoiceId}_{m}")).ToList();
            if (requested.Count == 0)
            {
                throw new BadRequestException("This voice already has every mood. Pick a mood to regenerate it");
            }
        }

        var job = jobs.GetSingle(x => x.BaseVoiceId == baseVoiceId);
        if (job is null)
        {
            job = DomainNpcVoiceJob.NewJob(baseVoiceId, baseDoc.OwnerId, requested);
            await jobs.Add(job);
        }
        else
        {
            foreach (var mood in requested)
            {
                var task = job.Moods.FirstOrDefault(m => m.Mood == mood);
                if (task is null)
                {
                    job.Moods.Add(new NpcMoodTask { Mood = mood });
                    continue;
                }

                task.Status = NpcMoodStatus.Pending;
                task.Error = null;
            }

            await jobs.Replace(job);
        }

        logger.LogAudit($"Queued {string.Join(", ", requested)} mood generation for NPC voice '{baseVoiceId}'");
        return job;
    }

    [HttpGet("{baseVoiceId}/job")]
    [Authorize]
    public ActionResult<DomainNpcVoiceJob> GetJob(string baseVoiceId)
    {
        var job = jobs.GetSingle(x => x.BaseVoiceId == baseVoiceId);
        return job is null ? NotFound() : job;
    }

    private bool CanManage(DomainNpcVoice voice) => voice.OwnerId == httpContextService.GetUserId() || httpContextService.UserHasPermission(Permissions.Admin);

    /// A recorded or deleted mood must not be overwritten later by a queued generation.
    private async Task DropFromJobAsync(string baseVoiceId, string mood)
    {
        var job = jobs.GetSingle(x => x.BaseVoiceId == baseVoiceId);
        if (job is null || job.Moods.RemoveAll(m => m.Mood == mood && m.Status == NpcMoodStatus.Pending) == 0)
        {
            return;
        }

        await jobs.Replace(job);
    }
}

public class GenerateMoodsRequest
{
    public List<string> Moods { get; set; }
}
