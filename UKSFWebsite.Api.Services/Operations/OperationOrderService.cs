using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Operations;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Services.Operations {
    public class OperationOrderService : DataBackedService<IOperationOrderDataService>, IOperationOrderService {
        public OperationOrderService(IOperationOrderDataService data) : base(data) { }

        public async Task Add(CreateOperationOrderRequest request) {
            Opord operation = new Opord {
                name = request.name,
                map = request.map,
                start = request.start.AddHours((double) request.starttime / 100),
                end = request.end.AddHours((double) request.endtime / 100),
                type = request.type
            };
            await Data().Add(operation);
        }
    }
}
