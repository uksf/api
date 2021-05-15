using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Command.Controllers
{
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class OperationReportController : Controller
    {
        private readonly IOperationReportContext _operationReportContext;
        private readonly IOperationReportService _operationReportService;

        public OperationReportController(IOperationReportService operationReportService, IOperationReportContext operationReportContext)
        {
            _operationReportService = operationReportService;
            _operationReportContext = operationReportContext;
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id)
        {
            Oprep oprep = _operationReportContext.GetSingle(id);
            return Ok(new { operationEntity = oprep, groupedAttendance = oprep.AttendanceReport.Users.GroupBy(x => x.GroupName) });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationReportRequest request)
        {
            await _operationReportService.Create(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Oprep request)
        {
            await _operationReportContext.Replace(request);
            return Ok();
        }

        [HttpGet, Authorize]
        public IActionResult Get()
        {
            return Ok(_operationReportContext.Get());
        }
    }
}
