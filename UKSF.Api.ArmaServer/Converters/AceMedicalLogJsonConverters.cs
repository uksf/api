using System.Text.Json;
using System.Text.Json.Serialization;
using UKSF.Api.ArmaServer.Models.Persistence;

namespace UKSF.Api.ArmaServer.Converters;

public class MedicalLogCategoryConverter : JsonConverter<MedicalLogCategory>
{
    public override MedicalLogCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadFromObject(ref reader, options);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray or StartObject for MedicalLogCategory, got {reader.TokenType}");
        }

        reader.Read();
        var logType = reader.GetString() ?? string.Empty;
        reader.Read();
        var entries = JsonSerializer.Deserialize<List<MedicalLogEntry>>(ref reader, options) ?? [];
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new MedicalLogCategory { LogType = logType, Entries = entries };
    }

    private static MedicalLogCategory ReadFromObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var category = new MedicalLogCategory();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            var name = reader.GetString();
            reader.Read();
            switch (name?.ToLowerInvariant())
            {
                case "logtype":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        category.LogType = reader.GetString() ?? string.Empty;
                    }
                    else
                    {
                        reader.Skip();
                    }

                    break;
                case "entries": category.Entries = JsonSerializer.Deserialize<List<MedicalLogEntry>>(ref reader, options) ?? []; break;
                default:        reader.Skip(); break;
            }
        }

        return category;
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
    public override MedicalLogEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadFromObject(ref reader);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray or StartObject for MedicalLogEntry, got {reader.TokenType}");
        }

        reader.Read();
        var message = ReadText(ref reader);
        reader.Read();
        var timestamp = ReadText(ref reader);
        reader.Read();
        var arguments = ReadArguments(ref reader);
        reader.Read();
        var logType = ReadText(ref reader);
        WoundEntryConverter.DrainToEndArray(ref reader);

        return new MedicalLogEntry
        {
            Message = message,
            Timestamp = timestamp,
            Arguments = arguments,
            LogType = logType
        };
    }

    private static MedicalLogEntry ReadFromObject(ref Utf8JsonReader reader)
    {
        var entry = new MedicalLogEntry();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            var name = reader.GetString();
            reader.Read();
            switch (name?.ToLowerInvariant())
            {
                case "message":   entry.Message = ReadText(ref reader); break;
                case "timestamp": entry.Timestamp = ReadText(ref reader); break;
                case "arguments": entry.Arguments = ReadArguments(ref reader); break;
                case "logtype":   entry.LogType = ReadText(ref reader); break;
                default:          reader.Skip(); break;
            }
        }

        return entry;
    }

    private static List<string> ReadArguments(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        var arguments = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            arguments.Add(ReadText(ref reader));
        }

        return arguments;
    }

    private static string ReadText(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String: return reader.GetString() ?? string.Empty;
            case JsonTokenType.Null:   return string.Empty;
            default:
                using (var element = JsonDocument.ParseValue(ref reader))
                {
                    return element.RootElement.GetRawText();
                }
        }
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
