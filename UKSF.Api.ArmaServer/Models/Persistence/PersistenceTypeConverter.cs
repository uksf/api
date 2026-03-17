using System.Text.Json;
using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

/// <summary>
/// Converts <see cref="JsonElement"/> values in <c>object</c>-typed properties to native .NET
/// types during System.Text.Json deserialization.  This prevents <see cref="JsonElement"/>
/// from reaching the MongoDB BSON serializer, which does not handle it.
///
/// Unlike <see cref="Core.Converters.InferredTypeConverter"/>, this converter does NOT
/// attempt date-time inference on strings — all strings are returned as-is.  It also
/// recursively converts arrays and objects so deeply-nested structures are fully unwrapped.
/// </summary>
public sealed class PersistenceTypeConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return ReadValue(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, object objectToWrite, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
    }

    private static object[] ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return list.ToArray();
            }

            list.Add(ReadValue(ref reader, options));
        }

        return list.ToArray();
    }

    private static Dictionary<string, object> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<string, object>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} when reading object property name");
            }

            var propertyName = reader.GetString();
            reader.Read();
            dictionary[propertyName] = ReadValue(ref reader, options);
        }

        return dictionary;
    }

    private static object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null                                          => null,
            JsonTokenType.True                                          => true,
            JsonTokenType.False                                         => false,
            JsonTokenType.Number when reader.TryGetInt64(out var value) => value,
            JsonTokenType.Number                                        => reader.GetDouble(),
            JsonTokenType.String                                        => reader.GetString(),
            JsonTokenType.StartArray                                    => ReadArray(ref reader, options),
            JsonTokenType.StartObject                                   => ReadObject(ref reader, options),
            _                                                           => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }
}
