using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Data.Personnel {
    public class LoaDataService : CachedDataService<Loa>, ILoaDataService {
        public LoaDataService(IMongoDatabase database, IEventBus dataEventBus) : base(database, dataEventBus, "loas") { }
    }
}
