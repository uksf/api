using System.Threading.Tasks;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Command.Services {
    public interface IOperationReportService {
        Task Create(CreateOperationReportRequest request);
    }

    public class OperationReportService : IOperationReportService {
        private readonly IAttendanceService _attendanceService;
        private readonly IOperationReportContext _operationReportContext;

        public OperationReportService(IOperationReportContext operationReportContext, IAttendanceService attendanceService) {
            _operationReportContext = operationReportContext;
            _attendanceService = attendanceService;
        }

        public async Task Create(CreateOperationReportRequest request) {
            Oprep operation = new() {
                Name = request.Name,
                Map = request.Map,
                Start = request.Start.AddHours((double) request.Starttime / 100),
                End = request.End.AddHours((double) request.Endtime / 100),
                Type = request.Type,
                Result = request.Result
            };
            operation.AttendanceReport = await _attendanceService.GenerateAttendanceReport(operation.Start, operation.End);
            await _operationReportContext.Add(operation);
        }
    }
}
