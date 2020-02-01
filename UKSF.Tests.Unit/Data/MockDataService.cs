using UKSF.Api.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;

namespace UKSF.Tests.Unit.Data {
    public class MockDataService : DataService<MockDataModel, IMockDataService>, IMockDataService {
        public MockDataService(IDataCollection dataCollection, IDataEventBus<IMockDataService> dataEventBus, string collectionName) : base(dataCollection, dataEventBus, collectionName) { }
    }
}
