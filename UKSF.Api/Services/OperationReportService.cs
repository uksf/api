﻿using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public interface IOperationReportService
{
    Task Create(CreateOperationReportRequest request);
}

public class OperationReportService : IOperationReportService
{
    private readonly IOperationReportContext _operationReportContext;

    public OperationReportService(IOperationReportContext operationReportContext)
    {
        _operationReportContext = operationReportContext;
    }

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
        await _operationReportContext.Add(operation);
    }
}
