using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace UKSF.Api.Tests.Common;

public static class TestUtilities
{
    extension<T>(UpdateDefinition<T> updateDefinition)
    {
        public BsonValue RenderUpdate()
        {
            return updateDefinition.Render(new RenderArgs<T>(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry));
        }
    }

    extension<T>(FilterDefinition<T> filterDefinition)
    {
        public BsonValue RenderFilter()
        {
            return filterDefinition.Render(new RenderArgs<T>(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry));
        }
    }
}
