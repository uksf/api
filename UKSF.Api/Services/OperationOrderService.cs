using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public interface IOperationOrderService
{
    Task Add(CreateOperationOrderRequest request);
}

public class OperationOrderService(IOperationOrderContext operationOrderContext) : IOperationOrderService
{
    public async Task Add(CreateOperationOrderRequest request)
    {
        DomainOpord operation = new()
        {
            Name = request.Name,
            Map = request.Map,
            Start = request.Start.AddHours((double)request.Starttime / 100),
            End = request.End.AddHours((double)request.Endtime / 100),
            Type = request.Type
        };
        await operationOrderContext.Add(operation);
    }
}
