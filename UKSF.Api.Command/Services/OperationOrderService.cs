using System.Threading.Tasks;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;

namespace UKSF.Api.Command.Services
{
    public interface IOperationOrderService
    {
        Task Add(CreateOperationOrderRequest request);
    }

    public class OperationOrderService : IOperationOrderService
    {
        private readonly IOperationOrderContext _operationOrderContext;

        public OperationOrderService(IOperationOrderContext operationOrderContext)
        {
            _operationOrderContext = operationOrderContext;
        }

        public async Task Add(CreateOperationOrderRequest request)
        {
            Opord operation = new()
            {
                Name = request.Name,
                Map = request.Map,
                Start = request.Start.AddHours((double) request.Starttime / 100),
                End = request.End.AddHours((double) request.Endtime / 100),
                Type = request.Type
            };
            await _operationOrderContext.Add(operation);
        }
    }
}
