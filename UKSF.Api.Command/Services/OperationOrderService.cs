using System.Threading.Tasks;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;

namespace UKSF.Api.Command.Services {
    public interface IOperationOrderService : IDataBackedService<IOperationOrderDataService> {
        Task Add(CreateOperationOrderRequest request);
    }

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
            await Data.Add(operation);
        }
    }
}
