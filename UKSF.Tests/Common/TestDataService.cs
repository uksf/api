using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;

namespace UKSF.Tests.Common {
    public class TestDataService : DataService<TestDataModel>, ITestDataService {
        public TestDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TestDataModel> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }
    }
}
