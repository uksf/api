using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.Tests.Common;

public class TestCachedContext : CachedMongoContext<TestDataModel>, ITestCachedContext
{
    public TestCachedContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(
        mongoCollectionFactory,
        eventBus,
        collectionName
    ) { }
}
