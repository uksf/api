using System.Threading.Tasks;
using UKSF.Api.Base.Context;
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
                Name = request.Name,
                Map = request.Map,
                Start = request.Start.AddHours((double) request.Starttime / 100),
                End = request.End.AddHours((double) request.Endtime / 100),
                Type = request.Type
            };
            await Data.Add(operation);
        }
    }
}
