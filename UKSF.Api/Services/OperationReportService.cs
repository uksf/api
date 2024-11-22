using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public interface IOperationReportService
{
    Task Create(CreateOperationReportRequest request);
}

public class OperationReportService(IOperationReportContext operationReportContext) : IOperationReportService
{
    public async Task Create(CreateOperationReportRequest request)
    {
        DomainOprep operation = new()
        {
            Name = request.Name,
            Map = request.Map,
            Start = request.Start.AddHours((double)request.Starttime / 100),
            End = request.End.AddHours((double)request.Endtime / 100),
            Type = request.Type,
            Result = request.Result
        };
        // operation.AttendanceReport = await _attendanceService.GenerateAttendanceReport(operation.Start, operation.End);
        await operationReportContext.Add(operation);
    }
}
