using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace UKSF.Api.Base.Context {
    public static class MongoClientFactory {
        public static IMongoDatabase GetDatabase(string connectionString) {
            ConventionPack conventionPack = new() { new IgnoreExtraElementsConvention(true), new IgnoreIfNullConvention(true) };
            ConventionRegistry.Register("DefaultConventions", conventionPack, _ => true);
            string database = MongoUrl.Create(connectionString).DatabaseName;
            return new MongoClient(connectionString).GetDatabase(database);
        }
    }
}
