using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Tests.Common;

public class TestCachedContext : CachedMongoContext<TestDataModel>, ITestCachedContext
{
    public TestCachedContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(
        mongoCollectionFactory,
        eventBus,
        collectionName
    ) { }
}
