using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models.Persistence;

namespace UKSF.Api.ArmaServer.Services;

public static class PersistenceMarkerMigration
{
    public static async Task<int> MigrateAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<BsonDocument>("persistenceSessions");
        var documents = await collection.Find(Builders<BsonDocument>.Filter.Exists("markers"))
                                        .Project(Builders<BsonDocument>.Projection.Include("markers"))
                                        .ToListAsync();
        var migrated = 0;
        foreach (var document in documents)
        {
            if (document["markers"] is not BsonArray markers || !markers.Any(x => x.IsBsonArray))
            {
                continue;
            }

            var structured = Restructure(markers);
            await collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
                Builders<BsonDocument>.Update.Set("markers", structured)
            );
            migrated++;
        }

        return migrated;
    }

    private static BsonArray Restructure(BsonArray markers)
    {
        var wrapper = new BsonDocument("markers", markers);
        List<PersistenceMarker> typed;
        using (var reader = new MongoDB.Bson.IO.BsonDocumentReader(wrapper))
        {
            reader.ReadStartDocument();
            reader.ReadName();
            typed = PersistenceMarkerListBsonSerializer.Instance.Deserialize(BsonDeserializationContext.CreateRoot(reader));
        }

        return new BsonArray(typed.Select(x => x.ToBsonDocument()));
    }
}
