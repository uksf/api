using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Tests.Common {
    public class TestContext : MongoContext<TestDataModel>, ITestContext {
        public TestContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<TestDataModel> dataEventBus, string collectionName) : base(
            mongoCollectionFactory,
            dataEventBus,
            collectionName
        ) { }
    }
}
