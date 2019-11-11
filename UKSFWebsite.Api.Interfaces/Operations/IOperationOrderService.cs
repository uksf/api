using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Interfaces.Operations {
    public interface IOperationOrderService {
        IOperationOrderDataService Data();
        Task Add(CreateOperationOrderRequest request);
    }
}
