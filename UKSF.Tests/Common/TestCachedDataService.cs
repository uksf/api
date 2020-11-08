using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;

namespace UKSF.Tests.Common {
    public class TestCachedDataService : CachedDataService<TestDataModel>, ITestCachedDataService {
        public TestCachedDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TestDataModel> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }
    }
}
