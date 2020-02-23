using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;

namespace UKSF.Api.Data {
    public class DataCollectionFactory : IDataCollectionFactory {
        private readonly IMongoDatabase database;

        public DataCollectionFactory(IMongoDatabase database) => this.database = database;

        public IDataCollection CreateDataCollection(string collectionName) {
            IDataCollection dataCollection = new DataCollection(database, collectionName);
            return dataCollection;
        }
    }
}
