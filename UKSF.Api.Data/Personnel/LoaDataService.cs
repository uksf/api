using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class LoaDataService : CachedDataService<Loa>, ILoaDataService {
        public LoaDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Loa> dataEventBus) : base(dataCollectionFactory, dataEventBus, "loas") { }
    }
}
