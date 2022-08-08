using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Command.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class OperationReportController : ControllerBase
{
    private readonly IOperationReportContext _operationReportContext;
    private readonly IOperationReportService _operationReportService;

    public OperationReportController(IOperationReportService operationReportService, IOperationReportContext operationReportContext)
    {
        _operationReportService = operationReportService;
        _operationReportContext = operationReportContext;
    }

    [HttpGet]
    [Authorize]
    public IEnumerable<Oprep> Get()
    {
        return _operationReportContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public OprepDataset Get(string id)
    {
        var oprep = _operationReportContext.GetSingle(id);
        return new() { OperationEntity = oprep, GroupedAttendance = oprep.AttendanceReport.Users.GroupBy(x => x.GroupName) };
    }

    [HttpPost]
    [Authorize]
    public async Task Post([FromBody] CreateOperationReportRequest request)
    {
        await _operationReportService.Create(request);
    }

    [HttpPut]
    [Authorize]
    public async Task Put([FromBody] Oprep request)
    {
        await _operationReportContext.Replace(request);
    }
}
