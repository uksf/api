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
        if (parentVoiceId is not null && context.GetSingle(x => x.VoiceId == parentVoiceId) is null)
        {
            throw new BadRequestException($"Parent voice '{parentVoiceId}' not found");
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

        if (context.GetSingle(x => x.VoiceId == voiceId) is not null)
        {
            throw new BadRequestException($"Voice '{voiceId}' already exists");
        }

        var filePath = await store.SaveAsync(voiceId, bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var doc = new DomainNpcVoice
        {
            VoiceId = voiceId,
            DisplayName = displayName,
            OwnerId = httpContextService.GetUserId(),
            MoodOf = parentVoiceId,
            FilePath = filePath,
            Sha256 = sha,
            DurationMs = wav.DurationMs
        };
        await context.Add(doc);

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

        if (doc.OwnerId != httpContextService.GetUserId() && !httpContextService.UserHasPermission(Permissions.Admin))
        {
            return Forbid();
        }

        store.Delete(doc.FilePath);
        await context.Delete(id);
        logger.LogAudit($"Deleted NPC voice '{doc.VoiceId}'");
        return Ok();
    }

    [HttpPost("{baseVoiceId}/generate-moods")]
    [Authorize]
    public async Task<DomainNpcVoiceJob> GenerateMoods(string baseVoiceId)
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

        if (baseDoc.OwnerId != httpContextService.GetUserId() && !httpContextService.UserHasPermission(Permissions.Admin))
        {
            throw new BadRequestException("Only the owner or an admin can generate moods for this voice");
        }

        var existing = jobs.GetSingle(x => x.BaseVoiceId == baseVoiceId);
        if (existing is not null)
        {
            await jobs.Delete(existing.Id); // re-run replaces the prior job
        }

        var job = DomainNpcVoiceJob.NewJob(baseVoiceId, baseDoc.OwnerId);
        await jobs.Add(job);
        logger.LogAudit($"Queued mood generation for NPC voice '{baseVoiceId}'");
        return job;
    }

    [HttpGet("{baseVoiceId}/job")]
    [Authorize]
    public ActionResult<DomainNpcVoiceJob> GetJob(string baseVoiceId)
    {
        var job = jobs.GetSingle(x => x.BaseVoiceId == baseVoiceId);
        return job is null ? NotFound() : job;
    }
}
