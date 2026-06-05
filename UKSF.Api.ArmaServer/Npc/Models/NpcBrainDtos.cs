using System.Text.Json;
using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Npc.Models;

public static class NpcBrainJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public class NpcTurnDto
{
    public string SpeakerId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public long T { get; set; }
}

public class NpcScriptedDto
{
    public List<NpcScriptedLine> Lines { get; set; } = [];
    public string Deflection { get; set; } = string.Empty;
}

public class RespondRequest
{
    public string NpcId { get; set; } = string.Empty;
    public NpcPersona Persona { get; set; } = new();
    public string Knowledge { get; set; } = string.Empty;
    public string Mode { get; set; } = "dynamic";
    public NpcScriptedDto Scripted { get; set; } // null for dynamic -> omitted by WhenWritingNull
    public string VoiceId { get; set; } = string.Empty;
    public string History { get; set; } = string.Empty;
    public List<NpcTurnDto> NewTurns { get; set; } = [];
    public string Provider { get; set; }
}

public class RespondResult
{
    public string Text { get; set; } = string.Empty;
    public string LineId { get; set; }
    public string AudioBase64 { get; set; }
    public long? DurationMs { get; set; }
    public string Provider { get; set; } = string.Empty;
}

public class PrerenderItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class PrerenderRequest
{
    public string VoiceId { get; set; } = string.Empty;
    public List<PrerenderItem> Items { get; set; } = [];
}

public class PrerenderResultItem
{
    public string Id { get; set; } = string.Empty;
    public string AudioBase64 { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

public class PrerenderResult
{
    public List<PrerenderResultItem> Items { get; set; } = [];
}
