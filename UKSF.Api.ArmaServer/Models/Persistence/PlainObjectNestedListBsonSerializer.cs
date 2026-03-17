namespace UKSF.Api.ArmaServer.Models.Persistence;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

public sealed class PlainObjectNestedListBsonSerializer : SerializerBase<List<List<object>>>
{
    public static readonly PlainObjectNestedListBsonSerializer Instance = new();

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, List<List<object>> value)
    {
        var writer = context.Writer;
        writer.WriteStartArray();
        foreach (var inner in value)
        {
            PlainBsonHelper.WriteArray(writer, inner);
        }

        writer.WriteEndArray();
    }

    public override List<List<object>> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        reader.ReadStartArray();
        var list = new List<List<object>>();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            list.Add(PlainBsonHelper.ReadArray(reader));
        }

        reader.ReadEndArray();
        return list;
    }
}
