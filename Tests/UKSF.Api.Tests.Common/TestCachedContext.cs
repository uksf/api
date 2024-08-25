using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Tests.Common;

public class TestCachedContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService, string collectionName)
    : CachedMongoContext<DomainTestModel>(mongoCollectionFactory, eventBus, variablesService, collectionName), ITestCachedContext;
