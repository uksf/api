using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface IOperationReportDataService : IDataService<Oprep, IOperationReportDataService> {
        Task Replace(Oprep oprep);
    }
}
