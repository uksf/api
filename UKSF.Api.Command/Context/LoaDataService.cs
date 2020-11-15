using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface ILoaDataService : IDataService<Loa>, ICachedDataService { }

    public class LoaDataService : CachedDataService<Loa>, ILoaDataService {
        public LoaDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Loa> dataEventBus) : base(dataCollectionFactory, dataEventBus, "loas") { }
    }
}
