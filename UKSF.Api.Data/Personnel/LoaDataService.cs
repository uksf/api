﻿using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class LoaDataService : CachedDataService<Loa, ILoaDataService>, ILoaDataService {
        public LoaDataService(IDataCollection dataCollection, IDataEventBus<ILoaDataService> dataEventBus) : base(dataCollection, dataEventBus, "loas") { }
    }
}