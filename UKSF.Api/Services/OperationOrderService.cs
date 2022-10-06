using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Services;

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
            Start = request.Start.AddHours((double)request.Starttime / 100),
            End = request.End.AddHours((double)request.Endtime / 100),
            Type = request.Type
        };
        await _operationOrderContext.Add(operation);
    }
}
