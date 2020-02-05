using UKSF.Api.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;

namespace UKSF.Tests.Unit.Data {
    public class MockCachedDataService : CachedDataService<MockDataModel, IMockCachedDataService>, IMockCachedDataService {
        public MockCachedDataService(IDataCollection dataCollection, IDataEventBus<IMockCachedDataService> dataEventBus, string collectionName) : base(dataCollection, dataEventBus, collectionName) { }
    }
}
