using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Npc.Models;

public static class NpcInteractionProfiles
{
    public const string Conversation = "conversation";
    public const string Guarded = "guarded";
}

public static class NpcCooperationBands
{
    public const string Closed = "closed";
    public const string Guarded = "guarded";
    public const string Engaged = "engaged";
    public const string Cooperative = "cooperative";

    public static readonly IReadOnlyList<string> Order = [Closed, Guarded, Engaged, Cooperative];

    public static int IndexOf(string band)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (string.Equals(Order[i], band, StringComparison.Ordinal)) return i;
        }

        return 1; // guarded
    }

    public static string Step(string band, int delta)
    {
        var next = Math.Clamp(IndexOf(band) + delta, 0, Order.Count - 1);
        return Order[next];
    }
}

public static class NpcGuardedTags
{
    public const string RelevantQuestion = "relevant_question";
    public const string Rapport = "rapport";
    public const string Pressure = "pressure";
    public const string Threat = "threat";
    public const string BackOff = "back_off";
    public const string AddressesConcern = "addresses_concern";
    public const string Other = "other";

    public static readonly HashSet<string> All =
    [
        RelevantQuestion, Rapport, Pressure, Threat, BackOff, AddressesConcern, Other
    ];

    public static bool IsKnown(string tag) => !string.IsNullOrEmpty(tag) && All.Contains(tag);

    public static string Normalise(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Other;
        var candidate = tag.Trim().ToLowerInvariant();
        return IsKnown(candidate) ? candidate : Other;
    }
}

public static class NpcGuardedDirectives
{
    public const string Normal = "normal";
    public const string Warn = "warn";
    public const string BackOff = "back_off";
    public const string Burned = "burned";
    public const string Refuse = "refuse";
    public const string Disclose = "disclose";
    public const string Safe = "safe";
}

public class NpcGuardedFact
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    /// Canonical speakable sentence. Engine-owned; never enters model prompts.
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class NpcGuardedConfig
{
    [JsonPropertyName("concern")]
    public string Concern { get; set; } = string.Empty;

    [JsonPropertyName("facts")]
    public List<NpcGuardedFact> Facts { get; set; } = [];
}

public class NpcGuardedState
{
    [JsonPropertyName("cooperationBand")]
    public string CooperationBand { get; set; } = NpcCooperationBands.Guarded;

    [JsonPropertyName("pendingWarning")]
    public bool PendingWarning { get; set; }

    [JsonPropertyName("burned")]
    public bool Burned { get; set; }

    [JsonPropertyName("disclosedFactIds")]
    public List<string> DisclosedFactIds { get; set; } = [];

    public NpcGuardedState Clone() =>
        new()
        {
            CooperationBand = CooperationBand,
            PendingWarning = PendingWarning,
            Burned = Burned,
            DisclosedFactIds = [..DisclosedFactIds]
        };
}

public class NpcGuardedClassification
{
    [JsonPropertyName("t")]
    public long T { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    /// 1-based fact slot, or null when the move is not topic-bound.
    [JsonPropertyName("topicSlot")]
    public int? TopicSlot { get; set; }

    /// Independent of Tag: true when this utterance also addresses the source concern.
    [JsonPropertyName("addressesConcern")]
    public bool AddressesConcern { get; set; }

    [JsonPropertyName("ambiguous")]
    public bool Ambiguous { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;
}

public class NpcGuardedEngineResult
{
    public NpcGuardedState NextState { get; set; } = new();
    public string Directive { get; set; } = NpcGuardedDirectives.Normal;
    public string PermittedFactId { get; set; }
    public string PermittedFactTopic { get; set; }
    public string PermittedFactText { get; set; }
    public List<NpcGuardedClassification> Classifications { get; set; } = [];
}

public class NpcGuardedClassifyRequest
{
    public string NpcId { get; set; } = string.Empty;
    public NpcPersona Persona { get; set; } = new();
    public string Concern { get; set; } = string.Empty;
    public List<(string Id, string Topic)> TopicCues { get; set; } = [];
    public NpcGuardedState State { get; set; } = new();
    public List<NpcTurnDto> Utterances { get; set; } = [];
}

public class NpcGuardedClassifyResult
{
    public List<NpcGuardedClassification> Classifications { get; set; } = [];
    public string Provider { get; set; } = string.Empty;
    public long Ms { get; set; }
}

public class NpcGuardedReplyRequest
{
    public string NpcId { get; set; } = string.Empty;
    public NpcPersona Persona { get; set; } = new();
    public string Knowledge { get; set; } = string.Empty;
    public List<NpcHistoryEntry> History { get; set; } = [];
    public List<NpcTurnDto> NewTurns { get; set; } = [];
    public string Directive { get; set; } = NpcGuardedDirectives.Normal;
    public string PermittedFactId { get; set; }
    public string PermittedFactTopic { get; set; }
    public string VoiceId { get; set; } = string.Empty;
}

public class NpcGuardedReplyModelOutput
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("mood")]
    public string Mood { get; set; } = "neutral";

    [JsonPropertyName("emote")]
    [JsonConverter(typeof(LooseOptionalStringConverter))]
    public string Emote { get; set; }

    [JsonPropertyName("disclosedFactId")]
    [JsonConverter(typeof(LooseOptionalStringConverter))]
    public string DisclosedFactId { get; set; }
}

public class NpcGuardedReplyResult
{
    public bool Ok { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Mood { get; set; } = "neutral";
    public string Emote { get; set; }
    public string DisclosedFactId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string VoiceId { get; set; }
    public long Ms { get; set; }
    public string Failure { get; set; }
}

public class NpcGuardedValidatedReply
{
    public bool Ok { get; set; }
    public string SpokenText { get; set; } = string.Empty;
    public string Mood { get; set; } = "neutral";
    public string Emote { get; set; }
    public string DisclosedFactId { get; set; }
    public string Failure { get; set; }
}

public class NpcGuardedTurnRequest
{
    public string NpcId { get; set; } = string.Empty;
    public NpcPersona Persona { get; set; } = new();
    public string Knowledge { get; set; } = string.Empty;
    public string Concern { get; set; } = string.Empty;
    public List<(string Id, string Topic)> TopicCues { get; set; } = [];
    public NpcGuardedState State { get; set; } = new();
    public List<NpcHistoryEntry> History { get; set; } = [];
    public List<NpcTurnDto> NewTurns { get; set; } = [];
    public string VoiceId { get; set; } = string.Empty;
}

public class NpcGuardedTurnResult
{
    public NpcGuardedClassifyResult Classify { get; set; }
    public NpcGuardedReplyResult Reply { get; set; }
}
