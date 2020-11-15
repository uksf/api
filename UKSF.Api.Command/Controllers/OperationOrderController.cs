using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Command.Controllers {
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class OperationOrderController : Controller {
        private readonly IOperationOrderService _operationOrderService;

        public OperationOrderController(IOperationOrderService operationOrderService) => _operationOrderService = operationOrderService;

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(_operationOrderService.Data.Get());

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) => Ok(new {result = _operationOrderService.Data.GetSingle(id)});

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationOrderRequest request) {
            await _operationOrderService.Add(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Opord request) {
            await _operationOrderService.Data.Replace(request);
            return Ok();
        }
    }
}
