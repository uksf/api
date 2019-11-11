using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Data.Operations {
    public class OperationReportDataService : CachedDataService<Oprep>, IOperationReportDataService {
        public OperationReportDataService(IMongoDatabase database, IEventBus dataEventBus) : base(database, dataEventBus, "oprep") { }

        public override List<Oprep> Get() {
            List<Oprep> reversed = base.Get();
            reversed.Reverse();
            return reversed;
        }

        public async Task Replace(Oprep request) {
            await Database.GetCollection<Oprep>(DatabaseCollection).ReplaceOneAsync(x => x.id == request.id, request);
            Refresh();
        }
    }
}
