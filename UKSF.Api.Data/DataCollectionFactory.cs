using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;

namespace UKSF.Api.Data {
    public class DataCollectionFactory : IDataCollectionFactory {
        private readonly IMongoDatabase database;

        public DataCollectionFactory(IMongoDatabase database) => this.database = database;

        public IDataCollection<T> CreateDataCollection<T>(string collectionName) {
            IDataCollection<T> dataCollection = new DataCollection<T>(database, collectionName);
            return dataCollection;
        }
    }
}
