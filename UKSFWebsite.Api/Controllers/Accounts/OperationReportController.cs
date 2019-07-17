using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Requests;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers.Accounts {
    [Route("[controller]"), Roles(RoleDefinitions.MEMBER)]
    public class OperationReportController : Controller {
        private readonly IOperationReportService operationReportService;

        public OperationReportController(IOperationReportService operationReportService) => this.operationReportService = operationReportService;

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) {
            Oprep oprep = operationReportService.GetSingle(id);
            return Ok(new {operationEntity = oprep, groupedAttendance = oprep.attendanceReport.users.GroupBy(x => x.groupName)});
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationReportRequest request) {
            await operationReportService.Create(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Oprep request) {
            await operationReportService.Replace(request);
            return Ok();
        }

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(operationReportService.Get());
    }
}
