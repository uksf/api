using System.Text.Json.Serialization;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Npc.Models;

public class DomainNpcSession : MongoObject
{
    [JsonPropertyName("npcId")]
    public string NpcId { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("persona")]
    public NpcPersona Persona { get; set; } = new();

    [JsonPropertyName("knowledge")]
    public string Knowledge { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "dynamic"; // "scripted" | "dynamic"

    /// conversation (default) | guarded. Missing on legacy docs → conversation.
    [JsonPropertyName("interactionProfile")]
    public string InteractionProfile { get; set; } = NpcInteractionProfiles.Conversation;

    [JsonPropertyName("scripted")]
    public NpcScripted Scripted { get; set; } = new();

    /// Authoring payload for guarded sources. Null/absent on conversation NPCs.
    [JsonPropertyName("guarded")]
    public NpcGuardedConfig Guarded { get; set; }

    /// Runtime cooperation/warning/burn/ledger. Null/absent on conversation NPCs.
    [JsonPropertyName("guardedState")]
    public NpcGuardedState GuardedState { get; set; }

    [JsonPropertyName("voiceId")]
    public string VoiceId { get; set; } = string.Empty;

    [JsonPropertyName("history")]
    public List<NpcHistoryEntry> History { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
