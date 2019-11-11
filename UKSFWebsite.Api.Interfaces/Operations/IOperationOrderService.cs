using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Interfaces.Operations {
    public interface IOperationOrderService : IDataBackedService<IOperationOrderDataService> {
        Task Add(CreateOperationOrderRequest request);
    }
}
