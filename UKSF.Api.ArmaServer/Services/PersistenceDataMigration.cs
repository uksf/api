using MongoDB.Bson;
using MongoDB.Driver;

namespace UKSF.Api.ArmaServer.Services;

public static class PersistenceDataMigration
{
    public static async Task MigrateAsync(IMongoDatabase database, Action<string> log = null)
    {
        var collection = database.GetCollection<BsonDocument>("persistenceSessions");
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

        log?.Invoke($"Found {documents.Count} documents to migrate");

        foreach (var doc in documents)
        {
            var key = doc.Contains("key") ? doc["key"].AsString : "unknown";
            var originalSize = doc.ToBson().Length;

            var migrated = UnwrapDiscriminators(doc).AsBsonDocument;

            var newSize = migrated.ToBson().Length;
            var reduction = originalSize - newSize;
            var percent = originalSize > 0 ? reduction * 100.0 / originalSize : 0;

            await collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), migrated);

            log?.Invoke($"Migrated '{key}': {originalSize} → {newSize} bytes ({reduction} bytes saved, {percent:F1}% reduction)");
        }

        log?.Invoke("Migration complete");
    }

    public static BsonValue UnwrapDiscriminators(BsonValue value)
    {
        if (value is BsonDocument doc)
        {
            if (doc.ElementCount == 2 && doc.Contains("_t") && doc.Contains("_v") && doc["_t"].BsonType == BsonType.String)
            {
                var typeName = doc["_t"].AsString;
                if (typeName.Contains("System.Object") || typeName.Contains("[]"))
                {
                    return UnwrapDiscriminators(doc["_v"]);
                }
            }

            var result = new BsonDocument();
            foreach (var element in doc)
            {
                result[element.Name] = UnwrapDiscriminators(element.Value);
            }

            return result;
        }

        if (value is BsonArray arr)
        {
            return new BsonArray(arr.Select(UnwrapDiscriminators));
        }

        return value;
    }
}
