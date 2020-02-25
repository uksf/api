using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace UKSF.Tests.Common {
    public static class TestUtilities {
        public static BsonValue Render<T>(UpdateDefinition<T> updateDefinition) => updateDefinition.Render(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);
    }
}
