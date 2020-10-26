using MongoDB.Driver;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Database {
    public interface IDataCollectionFactory {
        IDataCollection<T> CreateDataCollection<T>(string collectionName) where T : DatabaseObject;
    }

    public class DataCollectionFactory : IDataCollectionFactory {
        private readonly IMongoDatabase database;

        public DataCollectionFactory(IMongoDatabase database) => this.database = database;

        public IDataCollection<T> CreateDataCollection<T>(string collectionName) where T : DatabaseObject {
            IDataCollection<T> dataCollection = new DataCollection<T>(database, collectionName);
            return dataCollection;
        }
    }
}
