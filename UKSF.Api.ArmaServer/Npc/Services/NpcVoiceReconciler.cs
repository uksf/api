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
/// registry is registered again here, and a registry entry whose file moved to the
/// per-voice folder layout is repointed, so a database reset or a layout change costs
/// nothing.
public class NpcVoiceReconciler(INpcVoicesContext voicesContext, INpcVoiceStore store, IVariablesService variablesService, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var root = variablesService.GetVariable("NPC_VOICE_PATH")?.Item?.ToString();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            var restored = 0;
            var repointed = 0;
            foreach (var voiceDir in Directory.GetDirectories(root))
            {
                if (cancellationToken.IsCancellationRequested) return;

                var voiceId = Path.GetFileName(voiceDir);
                if (!File.Exists(Path.Combine(voiceDir, "ref.wav"))) continue;

                restored += await EnsureVoice(voiceId, null, NpcVoiceStore.BasePath(voiceId));

                foreach (var file in Directory.GetFiles(voiceDir, "*.wav"))
                {
                    var mood = Path.GetFileNameWithoutExtension(file);
                    if (mood == "ref" || !MoodScripts.Generated.Contains(mood)) continue;

                    restored += await EnsureVoice($"{voiceId}_{mood}", voiceId, NpcVoiceStore.VariantPath(voiceId, mood));
                }
            }

            if (restored > 0 || repointed > 0)
            {
                logger.LogInfo($"NPC voice registry: restored {restored} voice(s), repointed {repointed} stale path(s) from disk");
            }

            return;

            async Task<int> EnsureVoice(string voiceId, string moodOf, string relativePath)
            {
                var doc = voicesContext.GetSingle(x => x.VoiceId == voiceId);
                if (doc is not null)
                {
                    if (doc.FilePath == relativePath || store.Exists(doc.FilePath)) return 0;

                    doc.FilePath = relativePath;
                    await voicesContext.Replace(doc);
                    repointed++;
                    return 0;
                }

                var bytes = await store.ReadAsync(relativePath);
                if (bytes is null) return 0;

                await voicesContext.Add(
                    new DomainNpcVoice
                    {
                        VoiceId = voiceId,
                        DisplayName = voiceId,
                        OwnerId = string.Empty, // owner is not recoverable from disk; an admin can still manage it
                        MoodOf = moodOf,
                        FilePath = relativePath,
                        Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                        DurationMs = WavLoudness.DurationMs(bytes)
                    }
                );
                return 1;
            }
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to reconcile NPC voices from disk", exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
