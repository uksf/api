﻿using UKSF.Api.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;

namespace UKSF.Tests.Unit.Common {
    public class MockCachedDataService : CachedDataService<MockDataModel, IMockCachedDataService>, IMockCachedDataService {
        public MockCachedDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IMockCachedDataService> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }
    }
}