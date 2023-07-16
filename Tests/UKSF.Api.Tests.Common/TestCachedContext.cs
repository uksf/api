using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Tests.Common;

public class TestCachedContext : CachedMongoContext<TestDataModel>, ITestCachedContext
{
    public TestCachedContext(
        IMongoCollectionFactory mongoCollectionFactory,
        IEventBus eventBus,
        IVariablesService variablesService,
        string collectionName
    ) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        collectionName
    ) { }
}
