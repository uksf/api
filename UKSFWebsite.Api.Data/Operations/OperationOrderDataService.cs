using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Data.Operations {
    public class OperationOrderDataService : CachedDataService<Opord>, IOperationOrderDataService {
        public OperationOrderDataService(IMongoDatabase database) : base(database, "opord") { }

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
