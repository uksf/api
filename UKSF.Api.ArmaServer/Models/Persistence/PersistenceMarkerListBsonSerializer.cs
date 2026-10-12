namespace UKSF.Api.ArmaServer.Models.Persistence;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using UKSF.Api.ArmaServer.Converters;

public sealed class PersistenceMarkerListBsonSerializer : SerializerBase<List<PersistenceMarker>>
{
    public static readonly PersistenceMarkerListBsonSerializer Instance = new();

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, List<PersistenceMarker> value)
    {
        var markerSerializer = BsonSerializer.LookupSerializer<PersistenceMarker>();
        context.Writer.WriteStartArray();
        foreach (var marker in value)
        {
            markerSerializer.Serialize(context, marker);
        }

        context.Writer.WriteEndArray();
    }

    public override List<PersistenceMarker> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var markerSerializer = BsonSerializer.LookupSerializer<PersistenceMarker>();
        var reader = context.Reader;
        reader.ReadStartArray();
        var markers = new List<PersistenceMarker>();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            if (reader.CurrentBsonType != BsonType.Array)
            {
                markers.Add(markerSerializer.Deserialize(context));
                continue;
            }

            var legacy = PlainBsonHelper.ReadArray(reader);
            if (legacy.Count > 0)
            {
                markers.Add(PersistenceMarkerConverter.FromList(legacy));
            }
        }

        reader.ReadEndArray();
        return markers;
    }
}
