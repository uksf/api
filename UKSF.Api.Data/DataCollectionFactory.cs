using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models;

namespace UKSF.Api.Data {
    public class DataCollectionFactory : IDataCollectionFactory {
        private readonly IMongoDatabase database;

        public DataCollectionFactory(IMongoDatabase database) => this.database = database;

        public IDataCollection<T> CreateDataCollection<T>(string collectionName) where T : DatabaseObject {
            IDataCollection<T> dataCollection = new DataCollection<T>(database, collectionName);
            return dataCollection;
        }
    }
}
