using System.Text.Json;
using System.Text.Json.Serialization;
using UKSF.Api.ArmaServer.Models.Persistence;

namespace UKSF.Api.ArmaServer.Converters;

/// <summary>
/// ACE medical types are serialized as positional JSON arrays by CBA_fnc_encodeJSON.
/// These converters handle the array↔class mapping so BSON storage keeps named fields
/// while the JSON wire format stays compatible with upstream ACE.
/// Each Read drains any unexpected extra elements so future ACE schema additions
/// don't leave the reader mid-array.
/// </summary>
public class WoundEntryConverter : JsonConverter<WoundEntry>
{
    // SQF: [classComplex, amountOf, bleedingRate, woundDamage]
    public override WoundEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for WoundEntry, got {reader.TokenType}");
        }

        reader.Read();
        var classComplex = reader.GetInt32();
        reader.Read();
        var amountOf = reader.GetInt32();
        reader.Read();
        var bleedingRate = reader.GetDouble();
        reader.Read();
        var woundDamage = reader.GetDouble();
        DrainToEndArray(ref reader);

        return new WoundEntry
        {
            ClassComplex = classComplex,
            AmountOf = amountOf,
            BleedingRate = bleedingRate,
            WoundDamage = woundDamage
        };
    }

    public override void Write(Utf8JsonWriter writer, WoundEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.ClassComplex);
        writer.WriteNumberValue(value.AmountOf);
        writer.WriteNumberValue(value.BleedingRate);
        writer.WriteNumberValue(value.WoundDamage);
        writer.WriteEndArray();
    }

    internal static void DrainToEndArray(ref Utf8JsonReader reader)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }
    }
}

public class MedicationEntryConverter : JsonConverter<MedicationEntry>
{
    // SQF: [medication, timeOffset, timeToMaxEffect, maxTimeInSystem, hrAdjust, painAdjust, flowAdjust, dose]
    public override MedicationEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for MedicationEntry, got {reader.TokenType}");
        }

        reader.Read();
        var medication = reader.GetString() ?? string.Empty;
        reader.Read();
        var timeOffset = reader.GetDouble();
        reader.Read();
        var timeToMaxEffect = reader.GetDouble();
        reader.Read();
        var maxTimeInSystem = reader.GetDouble();
        reader.Read();
        var hrAdjust = reader.GetDouble();
        reader.Read();
        var painAdjust = reader.GetDouble();
        reader.Read();
        var flowAdjust = reader.GetDouble();
        reader.Read();
        var dose = reader.GetDouble();
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new MedicationEntry
        {
            Medication = medication,
            TimeOffset = timeOffset,
            TimeToMaxEffect = timeToMaxEffect,
            MaxTimeInSystem = maxTimeInSystem,
            HrAdjust = hrAdjust,
            PainAdjust = painAdjust,
            FlowAdjust = flowAdjust,
            Dose = dose
        };
    }

    public override void Write(Utf8JsonWriter writer, MedicationEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Medication);
        writer.WriteNumberValue(value.TimeOffset);
        writer.WriteNumberValue(value.TimeToMaxEffect);
        writer.WriteNumberValue(value.MaxTimeInSystem);
        writer.WriteNumberValue(value.HrAdjust);
        writer.WriteNumberValue(value.PainAdjust);
        writer.WriteNumberValue(value.FlowAdjust);
        writer.WriteNumberValue(value.Dose);
        writer.WriteEndArray();
    }
}

public class OccludedMedicationEntryConverter : JsonConverter<OccludedMedicationEntry>
{
    // SQF: [partIndex, className]
    public override OccludedMedicationEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for OccludedMedicationEntry, got {reader.TokenType}");
        }

        reader.Read();
        var partIndex = reader.GetInt32();
        reader.Read();
        var className = reader.GetString() ?? string.Empty;
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new OccludedMedicationEntry { PartIndex = partIndex, ClassName = className };
    }

    public override void Write(Utf8JsonWriter writer, OccludedMedicationEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.PartIndex);
        writer.WriteStringValue(value.ClassName);
        writer.WriteEndArray();
    }
}

public class IvBagEntryConverter : JsonConverter<IvBagEntry>
{
    // SQF: [volume, type, partIndex, treatment, rateCoef, item]
    public override IvBagEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for IvBagEntry, got {reader.TokenType}");
        }

        reader.Read();
        var volume = reader.GetDouble();
        reader.Read();
        var type = reader.GetString() ?? string.Empty;
        reader.Read();
        var partIndex = reader.GetInt32();
        reader.Read();
        var treatment = reader.GetString() ?? string.Empty;
        reader.Read();
        var rateCoef = reader.GetDouble();
        reader.Read();
        var item = reader.GetString() ?? string.Empty;
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new IvBagEntry
        {
            Volume = volume,
            Type = type,
            PartIndex = partIndex,
            Treatment = treatment,
            RateCoef = rateCoef,
            Item = item
        };
    }

    public override void Write(Utf8JsonWriter writer, IvBagEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Volume);
        writer.WriteStringValue(value.Type);
        writer.WriteNumberValue(value.PartIndex);
        writer.WriteStringValue(value.Treatment);
        writer.WriteNumberValue(value.RateCoef);
        writer.WriteStringValue(value.Item);
        writer.WriteEndArray();
    }
}

public class TriageCardEntryConverter : JsonConverter<TriageCardEntry>
{
    // SQF: [item, count, timestamp]
    public override TriageCardEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for TriageCardEntry, got {reader.TokenType}");
        }

        reader.Read();
        var item = reader.GetString() ?? string.Empty;
        reader.Read();
        var count = reader.GetInt32();
        reader.Read();
        var timestamp = reader.GetDouble();
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new TriageCardEntry
        {
            Item = item,
            Count = count,
            Timestamp = timestamp
        };
    }

    public override void Write(Utf8JsonWriter writer, TriageCardEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Item);
        writer.WriteNumberValue(value.Count);
        writer.WriteNumberValue(value.Timestamp);
        writer.WriteEndArray();
    }
}

public class MedicalLogCategoryConverter : JsonConverter<MedicalLogCategory>
{
    // SQF: [varName, [[message, timeStamp, arguments, logType], ...]]
    public override MedicalLogCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for MedicalLogCategory, got {reader.TokenType}");
        }

        reader.Read();
        var logType = reader.GetString() ?? string.Empty;
        reader.Read(); // StartArray for entries
        var entries = JsonSerializer.Deserialize<List<MedicalLogEntry>>(ref reader, options) ?? [];
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new MedicalLogCategory { LogType = logType, Entries = entries };
    }

    public override void Write(Utf8JsonWriter writer, MedicalLogCategory value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.LogType);
        JsonSerializer.Serialize(writer, value.Entries, options);
        writer.WriteEndArray();
    }
}

public class MedicalLogEntryConverter : JsonConverter<MedicalLogEntry>
{
    // SQF: [message, timeStamp, arguments, logType]
    public override MedicalLogEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray for MedicalLogEntry, got {reader.TokenType}");
        }

        reader.Read();
        var message = reader.GetString() ?? string.Empty;
        reader.Read();
        var timestamp = reader.GetString() ?? string.Empty;
        reader.Read(); // StartArray for arguments
        var arguments = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? [];
        reader.Read();
        var logType = reader.GetString() ?? string.Empty;
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new MedicalLogEntry
        {
            Message = message,
            Timestamp = timestamp,
            Arguments = arguments,
            LogType = logType
        };
    }

    public override void Write(Utf8JsonWriter writer, MedicalLogEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Message);
        writer.WriteStringValue(value.Timestamp);
        JsonSerializer.Serialize(writer, value.Arguments, options);
        writer.WriteStringValue(value.LogType);
        writer.WriteEndArray();
    }
}
