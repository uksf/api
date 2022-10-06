using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace UKSF.Api.Tests.Common;

public static class TestUtilities
{
    public static BsonValue RenderUpdate<T>(this UpdateDefinition<T> updateDefinition)
    {
        return updateDefinition.Render(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);
    }

    public static BsonValue RenderFilter<T>(this FilterDefinition<T> filterDefinition)
    {
        return filterDefinition.Render(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);
    }
}
