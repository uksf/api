﻿using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Context {
    public interface IAccountDataService : IDataService<Account>, ICachedDataService { }

    public class AccountDataService : CachedDataService<Account>, IAccountDataService {
        public AccountDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Account> dataEventBus) : base(dataCollectionFactory, dataEventBus, "accounts") { }
    }
}