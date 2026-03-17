using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class WoundEntry
{
    [JsonPropertyName("classComplex")]
    public int ClassComplex { get; set; }

    [JsonPropertyName("amountOf")]
    public int AmountOf { get; set; }

    [JsonPropertyName("bleedingRate")]
    public double BleedingRate { get; set; }

    [JsonPropertyName("woundDamage")]
    public double WoundDamage { get; set; }
}

public class MedicationEntry
{
    [JsonPropertyName("medication")]
    public string Medication { get; set; } = string.Empty;

    [JsonPropertyName("timeOffset")]
    public double TimeOffset { get; set; }

    [JsonPropertyName("timeToMaxEffect")]
    public double TimeToMaxEffect { get; set; }

    [JsonPropertyName("maxTimeInSystem")]
    public double MaxTimeInSystem { get; set; }

    [JsonPropertyName("hrAdjust")]
    public double HrAdjust { get; set; }

    [JsonPropertyName("painAdjust")]
    public double PainAdjust { get; set; }

    [JsonPropertyName("flowAdjust")]
    public double FlowAdjust { get; set; }

    [JsonPropertyName("dose")]
    public double Dose { get; set; }
}

public class OccludedMedicationEntry
{
    [JsonPropertyName("partIndex")]
    public int PartIndex { get; set; }

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;
}

public class IvBagEntry
{
    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("partIndex")]
    public int PartIndex { get; set; }

    [JsonPropertyName("treatment")]
    public string Treatment { get; set; } = string.Empty;

    [JsonPropertyName("rateCoef")]
    public double RateCoef { get; set; }

    [JsonPropertyName("item")]
    public string Item { get; set; } = string.Empty;
}

public class TriageCardEntry
{
    [JsonPropertyName("item")]
    public string Item { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }
}

public class MedicalLogCategory
{
    [JsonPropertyName("logType")]
    public string LogType { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public List<MedicalLogEntry> Entries { get; set; } = [];
}

public class MedicalLogEntry
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = [];

    [JsonPropertyName("logType")]
    public string LogType { get; set; } = string.Empty;
}
