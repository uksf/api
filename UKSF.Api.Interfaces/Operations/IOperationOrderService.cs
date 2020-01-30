using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Interfaces.Operations {
    public interface IOperationOrderService : IDataBackedService<IOperationOrderDataService> {
        Task Add(CreateOperationOrderRequest request);
    }
}
