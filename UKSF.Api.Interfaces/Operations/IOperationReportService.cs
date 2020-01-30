using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Interfaces.Operations {
    public interface IOperationReportService : IDataBackedService<IOperationReportDataService> {
        Task Create(CreateOperationReportRequest request);
    }
}
