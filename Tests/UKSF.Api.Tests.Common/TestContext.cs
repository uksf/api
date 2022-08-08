using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Tests.Common;

public class TestContext : MongoContext<TestDataModel>, ITestContext
{
    public TestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(
        mongoCollectionFactory,
        eventBus,
        collectionName
    ) { }
}
