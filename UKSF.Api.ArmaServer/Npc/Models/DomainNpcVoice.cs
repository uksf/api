using System.Text.Json.Serialization;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Npc.Models;

public class DomainNpcVoice : MongoObject
{
    [JsonPropertyName("voiceId")]
    public string VoiceId { get; set; } = string.Empty; // registry slug, unique (^[A-Za-z0-9_-]+$)

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("moodOf")]
    public string MoodOf { get; set; } // parent voiceId when this is a mood variant; null for a base voice

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty; // relative to NPC_VOICE_PATH (master WAV)

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
