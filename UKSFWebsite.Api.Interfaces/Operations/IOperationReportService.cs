using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Operations;

namespace UKSFWebsite.Api.Interfaces.Operations {
    public interface IOperationReportService : IDataBackedService<IOperationReportDataService> {
        Task Create(CreateOperationReportRequest request);
    }
}
