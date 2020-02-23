using UKSF.Api.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;

namespace UKSF.Tests.Unit.Data {
    public class MockDataService : DataService<MockDataModel, IMockDataService>, IMockDataService {
        public MockDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IMockDataService> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }
    }
}
