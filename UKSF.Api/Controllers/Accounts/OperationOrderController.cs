using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Operations;
using UKSF.Api.Models.Operations;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers.Accounts {
    [Route("[controller]"), Roles(RoleDefinitions.MEMBER)]
    public class OperationOrderController : Controller {
        private readonly IOperationOrderService operationOrderService;

        public OperationOrderController(IOperationOrderService operationOrderService) => this.operationOrderService = operationOrderService;

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(operationOrderService.Data().Get());

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) => Ok(new {result = operationOrderService.Data().GetSingle(id)});

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationOrderRequest request) {
            await operationOrderService.Add(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Opord request) {
            await operationOrderService.Data().Replace(request);
            return Ok();
        }
    }
}
