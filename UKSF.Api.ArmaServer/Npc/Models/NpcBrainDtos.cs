using System;
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

public sealed class LooseOptionalStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        if (reader.TokenType is JsonTokenType.Null or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Number)
        {
            reader.Skip();
            return null;
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

public class NpcTurnDto
{
    public string SpeakerId { get; set; } = string.Empty;

    /// The player's spoken name. The UID identifies nobody to the brain; the name is what
    /// lets a follow-up like "do you know?" attach to the right thread.
    public string SpeakerName { get; set; } = string.Empty;

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
    public List<NpcHistoryEntry> History { get; set; } = [];
    public List<NpcTurnDto> NewTurns { get; set; } = [];

    /// When set, the brain returns text and mood without synthesising audio; the
    /// caller streams the line itself. Used by the dynamic streaming turn.
    public bool TextOnly { get; set; }

    /// Set when the address check could not decide whether this NPC was spoken to.
    /// The brain may then decline the turn by answering with [none] alone.
    public bool MayNotBeAddressed { get; set; }

    public string Provider { get; set; }
}

public class RespondResult
{
    public string Text { get; set; } = string.Empty;
    public string LineId { get; set; }
    public string AudioBase64 { get; set; }
    public long? DurationMs { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Mood { get; set; } = "neutral";

    /// Resolved voice for this turn ({base} or {base}_{mood}). Set when TextOnly
    /// asks the brain to skip synthesis so the caller can stream the line itself.
    public string VoiceId { get; set; }
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
