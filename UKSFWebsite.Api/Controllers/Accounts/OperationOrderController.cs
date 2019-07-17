using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Requests;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers.Accounts {
    [Route("[controller]"), Roles(RoleDefinitions.MEMBER)]
    public class OperationOrderController : Controller {
        private readonly IOperationOrderService operationOrderService;

        public OperationOrderController(IOperationOrderService operationOrderService) => this.operationOrderService = operationOrderService;

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(operationOrderService.Get());

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) => Ok(new {result = operationOrderService.GetSingle(id)});

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationOrderRequest request) {
            await operationOrderService.Add(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Opord request) {
            await operationOrderService.Replace(request);
            return Ok();
        }
    }
}
