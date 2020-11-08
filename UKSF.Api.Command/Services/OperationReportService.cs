using System.Threading.Tasks;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Command.Services {
    public interface IOperationReportService : IDataBackedService<IOperationReportDataService> {
        Task Create(CreateOperationReportRequest request);
    }

    public class OperationReportService : DataBackedService<IOperationReportDataService>, IOperationReportService {
        private readonly IAttendanceService attendanceService;

        public OperationReportService(IOperationReportDataService data, IAttendanceService attendanceService) : base(data) => this.attendanceService = attendanceService;

        public async Task Create(CreateOperationReportRequest request) {
            Oprep operation = new Oprep {
                name = request.name,
                map = request.map,
                start = request.start.AddHours((double) request.starttime / 100),
                end = request.end.AddHours((double) request.endtime / 100),
                type = request.type,
                result = request.result
            };
            operation.attendanceReport = await attendanceService.GenerateAttendanceReport(operation.start, operation.end);
            await Data.Add(operation);
        }
    }
}
