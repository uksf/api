using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface IOperationOrderDataService : IDataService<Opord, IOperationOrderDataService> {
        Task Replace(Opord opord);
    }
}
