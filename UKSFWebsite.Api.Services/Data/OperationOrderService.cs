using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Requests;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class OperationOrderService : CachedDataService<Opord>, IOperationOrderService {
        public OperationOrderService(IMongoDatabase database) : base(database, "opord") { }

        public async Task Add(CreateOperationOrderRequest request) {
            Opord operation = new Opord {
                name = request.name,
                map = request.map,
                start = request.start.AddHours((double) request.starttime / 100),
                end = request.end.AddHours((double) request.endtime / 100),
                type = request.type
            };
            await base.Add(operation);
        }

        public override List<Opord> Get() {
            List<Opord> reversed = base.Get();
            reversed.Reverse();
            return reversed;
        }

        public async Task Replace(Opord request) {
            await Database.GetCollection<Opord>(DatabaseCollection).ReplaceOneAsync(x => x.id == request.id, request);
        }
    }
}
