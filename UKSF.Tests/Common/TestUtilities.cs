using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace UKSF.Tests.Common {
    public static class TestUtilities {
        public static BsonValue RenderUpdate<T>(UpdateDefinition<T> updateDefinition) => updateDefinition.Render(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);
        public static BsonValue RenderFilter<T>(FilterDefinition<T> filterDefinition) => filterDefinition.Render(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);
    }
}
