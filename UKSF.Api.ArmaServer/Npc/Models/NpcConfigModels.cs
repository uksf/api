using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Npc.Models;

public class NpcPersona
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("mood")]
    public string Mood { get; set; } = string.Empty;

    [JsonPropertyName("attitudeToPlayers")]
    public string AttitudeToPlayers { get; set; } = string.Empty;
}

public class NpcScriptedLine
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public string Line { get; set; } = string.Empty;
}

public class NpcScripted
{
    [JsonPropertyName("lines")]
    public List<NpcScriptedLine> Lines { get; set; } = [];

    [JsonPropertyName("deflection")]
    public string Deflection { get; set; } = string.Empty;
}

// One entry in the rolling conversation history.
public class NpcHistoryEntry
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty; // "player" | "npc"

    [JsonPropertyName("speaker")]
    public string Speaker { get; set; } = string.Empty; // player id/name; "" for npc

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("t")]
    public long T { get; set; } // epoch ms
}
