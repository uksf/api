using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Data.Personnel {
    public class LoaDataService : CachedDataService<Loa, ILoaDataService>, ILoaDataService {
        public LoaDataService(IMongoDatabase database, IDataEventBus<ILoaDataService> dataEventBus) : base(database, dataEventBus, "loas") { }
    }
}
