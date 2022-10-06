using MongoDB.Driver;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context.Base;

public interface IMongoCollectionFactory
{
    IMongoCollection<T> CreateMongoCollection<T>(string collectionName) where T : MongoObject;
}

public class MongoCollectionFactory : IMongoCollectionFactory
{
    private readonly IMongoDatabase _database;

    public MongoCollectionFactory(IMongoDatabase database)
    {
        _database = database;
    }

    public IMongoCollection<T> CreateMongoCollection<T>(string collectionName) where T : MongoObject
    {
        IMongoCollection<T> mongoCollection = new MongoCollection<T>(_database, collectionName);
        return mongoCollection;
    }
}
