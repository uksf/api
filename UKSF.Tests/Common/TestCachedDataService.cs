using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;

namespace UKSF.Tests.Common {
    public class TestCachedDataService : CachedDataService<TestDataModel>, ITestCachedDataService {
        public TestCachedDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TestDataModel> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }
    }
}
