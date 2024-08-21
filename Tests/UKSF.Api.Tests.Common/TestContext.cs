using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.Tests.Common;

public class TestContext : MongoContext<DomainTestModel>, ITestContext
{
    public TestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(
        mongoCollectionFactory,
        eventBus,
        collectionName
    ) { }
}
