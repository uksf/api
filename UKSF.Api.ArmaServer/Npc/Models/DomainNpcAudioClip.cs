using System.Text.Json.Serialization;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Npc.Models;

// A pre-rendered clip: scripted line, deflection, or filler. Keyed per-NPC by (SessionId, NpcId, ClipId)
// so two NPCs sharing a voice can't clobber each other's scripted lines. Persisted so a mid-mission
// API restart keeps them. Audio bytes live on disk under NPC_AUDIO_PATH; this doc carries the relative path.
public class DomainNpcAudioClip : MongoObject
{
    [JsonPropertyName("npcId")]
    public string NpcId { get; set; } = string.Empty;

    [JsonPropertyName("voiceId")]
    public string VoiceId { get; set; } = string.Empty;

    [JsonPropertyName("clipId")]
    public string ClipId { get; set; } = string.Empty; // line id, "__deflection__", or "f0".."f3"

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty; // relative to NPC_AUDIO_PATH, e.g. "2026-06-07/session1_npc1_ammo.wav"

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty; // mission session, for cleanup
}
