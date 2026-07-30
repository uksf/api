using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Rebuilds the voice registry from the master WAVs on disk at startup.
///
/// The registry is the only part of a voice that lives solely in Mongo; the audio itself
/// is on disk and in the clacks voice store. The dev database is shared and is reset from
/// time to time, and when it went the NPCs fell silent with their audio still sitting on
/// disk, needing a manual re-upload. Anything present on disk but missing from the
/// registry is registered again here, so a database reset costs nothing.
public class NpcVoiceReconciler(INpcVoicesContext voicesContext, INpcVoiceStore store, IVariablesService variablesService, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var root = variablesService.GetVariable("NPC_VOICE_PATH")?.Item?.ToString();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            var restored = 0;
            foreach (var file in Directory.GetFiles(root, "*.wav"))
            {
                if (cancellationToken.IsCancellationRequested) return;

                var voiceId = Path.GetFileNameWithoutExtension(file);
                if (voicesContext.GetSingle(x => x.VoiceId == voiceId) is not null) continue;

                var bytes = await store.ReadAsync(Path.GetFileName(file));
                if (bytes is null) continue;

                await voicesContext.Add(
                    new DomainNpcVoice
                    {
                        VoiceId = voiceId,
                        DisplayName = voiceId,
                        OwnerId = string.Empty, // owner is not recoverable from disk; an admin can still manage it
                        MoodOf = MoodOf(voiceId),
                        FilePath = Path.GetFileName(file),
                        Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                        DurationMs = 0
                    }
                );
                restored++;
            }

            if (restored > 0)
            {
                logger.LogInfo($"NPC voice registry: restored {restored} voice(s) from disk");
            }
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to reconcile NPC voices from disk", exception);
        }
    }

    /// A mood variant is named {base}_{mood}, and its base must already be on disk —
    /// otherwise the underscore belongs to the voice's own name.
    private string MoodOf(string voiceId)
    {
        var mood = MoodScripts.Generated.FirstOrDefault(m => m != MoodScripts.Neutral && voiceId.EndsWith($"_{m}", StringComparison.Ordinal));
        if (mood is null && voiceId.EndsWith($"_{MoodScripts.Neutral}", StringComparison.Ordinal)) mood = MoodScripts.Neutral;

        return mood is null ? null : voiceId[..^(mood.Length + 1)];
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
