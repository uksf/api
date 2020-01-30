using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationReportDataService : CachedDataService<Oprep, IOperationReportDataService>, IOperationReportDataService {
        public OperationReportDataService(IMongoDatabase database, IDataEventBus<IOperationReportDataService> dataEventBus) : base(database, dataEventBus, "oprep") { }

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
