namespace UKSF.Api.ArmaServer.Models.Persistence;

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

public sealed class PlainObjectDictionaryBsonSerializer : SerializerBase<Dictionary<string, object>>
{
    public static readonly PlainObjectDictionaryBsonSerializer Instance = new();

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Dictionary<string, object> value)
    {
        PlainBsonHelper.WriteDocument(context.Writer, value);
    }

    public override Dictionary<string, object> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return PlainBsonHelper.ReadDocument(context.Reader);
    }
}
