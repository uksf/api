using System.Threading.Tasks;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Requests;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IOperationOrderService : IDataService<Opord> {
        Task Add(CreateOperationOrderRequest request);
        Task Replace(Opord request);
    }
}
