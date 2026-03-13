using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace UKSF.Api.ArmaServer.Models.Persistence;

/// <summary>
/// Safety-net BSON serializer for <see cref="JsonElement"/>.  If any <see cref="JsonElement"/>
/// value slips past the <see cref="PersistenceTypeConverter"/> and reaches the MongoDB driver,
/// this serializer converts it to the appropriate BSON type instead of throwing.
/// </summary>
public sealed class JsonElementBsonSerializer : SerializerBase<JsonElement>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonElement value)
    {
        WriteToBson(context.Writer, value);
    }

    public override JsonElement Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonValue = BsonValueSerializer.Instance.Deserialize(context, args);
        var json = bsonValue.ToJson();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static void WriteToBson(IBsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartDocument();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WriteName(property.Name);
                    WriteToBson(writer, property.Value);
                }

                writer.WriteEndDocument();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteToBson(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String: writer.WriteString(element.GetString()!); break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                {
                    writer.WriteInt64(longValue);
                }
                else
                {
                    writer.WriteDouble(element.GetDouble());
                }

                break;

            case JsonValueKind.True: writer.WriteBoolean(true); break;

            case JsonValueKind.False: writer.WriteBoolean(false); break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default: writer.WriteNull(); break;
        }
    }
}
