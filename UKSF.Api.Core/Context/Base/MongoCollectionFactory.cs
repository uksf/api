using MongoDB.Driver;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context.Base;

public interface IMongoCollectionFactory
{
    IMongoCollection<T> CreateMongoCollection<T>(string collectionName) where T : MongoObject;
}

public class MongoCollectionFactory(IMongoDatabase database) : IMongoCollectionFactory
{
    public IMongoCollection<T> CreateMongoCollection<T>(string collectionName) where T : MongoObject
    {
        IMongoCollection<T> mongoCollection = new MongoCollection<T>(database, collectionName);
        return mongoCollection;
    }
}
