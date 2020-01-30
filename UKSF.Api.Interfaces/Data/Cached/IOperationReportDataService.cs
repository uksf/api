using System.Threading.Tasks;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IOperationReportDataService : IDataService<Oprep, IOperationReportDataService> {
        Task Replace(Oprep oprep);
    }
}
