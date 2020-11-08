using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;

namespace UKSF.Api.Command.Controllers {
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class OperationReportController : Controller {
        private readonly IOperationReportService operationReportService;

        public OperationReportController(IOperationReportService operationReportService) => this.operationReportService = operationReportService;

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) {
            Oprep oprep = operationReportService.Data.GetSingle(id);
            return Ok(new {operationEntity = oprep, groupedAttendance = oprep.attendanceReport.users.GroupBy(x => x.groupName)});
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationReportRequest request) {
            await operationReportService.Create(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Oprep request) {
            await operationReportService.Data.Replace(request);
            return Ok();
        }

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(operationReportService.Data.Get());
    }
}
