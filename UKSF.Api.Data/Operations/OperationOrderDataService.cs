using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationOrderDataService : CachedDataService<Opord, IOperationOrderDataService>, IOperationOrderDataService {
        public OperationOrderDataService(IMongoDatabase database, IDataEventBus<IOperationOrderDataService> dataEventBus) : base(database, dataEventBus, "opord") { }

        public override List<Opord> Get() {
            List<Opord> reversed = base.Get();
            reversed.Reverse();
            return reversed;
        }

        public async Task Replace(Opord opord) {
            await Database.GetCollection<Opord>(DatabaseCollection).ReplaceOneAsync(x => x.id == opord.id, opord);
            Refresh();
        }
    }
}
