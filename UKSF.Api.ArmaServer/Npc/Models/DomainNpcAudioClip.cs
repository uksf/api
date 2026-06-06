using System.Text.Json.Serialization;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Npc.Models;

// A pre-rendered clip: scripted line, deflection, or filler. Keyed per-NPC by (SessionId, NpcId, ClipId)
// so two NPCs sharing a voice can't clobber each other's scripted lines. Persisted so a mid-mission
// API restart keeps them.
public class DomainNpcAudioClip : MongoObject
{
    [JsonPropertyName("npcId")]
    public string NpcId { get; set; } = string.Empty;

    [JsonPropertyName("voiceId")]
    public string VoiceId { get; set; } = string.Empty;

    [JsonPropertyName("clipId")]
    public string ClipId { get; set; } = string.Empty; // line id, "__deflection__", or "f0".."f3"

    [JsonPropertyName("audioBase64")]
    public string AudioBase64 { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty; // mission session, for cleanup
}
