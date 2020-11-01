using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services.Data {
    public interface ILoaDataService : IDataService<Loa>, ICachedDataService { }

    public class LoaDataService : CachedDataService<Loa>, ILoaDataService {
        public LoaDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Loa> dataEventBus) : base(dataCollectionFactory, dataEventBus, "loas") { }
    }
}
