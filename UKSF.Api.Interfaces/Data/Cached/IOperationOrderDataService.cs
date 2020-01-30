using System.Threading.Tasks;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IOperationOrderDataService : IDataService<Opord, IOperationOrderDataService> {
        Task Replace(Opord opord);
    }
}
