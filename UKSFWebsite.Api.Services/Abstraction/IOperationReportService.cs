using System.Threading.Tasks;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Requests;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IOperationReportService : IDataService<Oprep> {
        Task Create(CreateOperationReportRequest request);
        Task Replace(Oprep request);
    }
}
