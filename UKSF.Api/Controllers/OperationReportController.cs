using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class OperationReportController(IOperationReportService operationReportService, IOperationReportContext operationReportContext) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainOprep> Get()
    {
        return operationReportContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public OprepDataset Get([FromRoute] string id)
    {
        var oprep = operationReportContext.GetSingle(id);
        return new OprepDataset { OperationEntity = oprep, GroupedAttendance = oprep.AttendanceReport.Users.GroupBy(x => x.GroupName) };
    }

    [HttpPost]
    [Authorize]
    public async Task Post([FromBody] CreateOperationReportRequest request)
    {
        await operationReportService.Create(request);
    }

    [HttpPut]
    [Authorize]
    public async Task Put([FromBody] DomainOprep request)
    {
        await operationReportContext.Replace(request);
    }
}
