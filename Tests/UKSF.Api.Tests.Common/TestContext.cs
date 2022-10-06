using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Tests.Common;

public class TestContext : MongoContext<TestDataModel>, ITestContext
{
    public TestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(
        mongoCollectionFactory,
        eventBus,
        collectionName
    ) { }
}
