using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Operations;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Services.Operations {
    public class OperationReportService : IOperationReportService {
        private readonly IAttendanceService attendanceService;
        private readonly IOperationReportDataService data;

        public OperationReportService(IOperationReportDataService data, IAttendanceService attendanceService) {
            this.data = data;
            this.attendanceService = attendanceService;
        }

        public IOperationReportDataService Data() => data;

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
            await data.Add(operation);
        }
    }
}
