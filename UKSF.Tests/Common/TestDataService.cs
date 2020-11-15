using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Tests.Common {
    public class TestDataService : DataService<TestDataModel>, ITestDataService {
        public TestDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TestDataModel> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }
    }
}
