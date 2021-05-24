using System.Collections.Generic;
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
    public class OperationOrderController : ControllerBase
    {
        private readonly IOperationOrderContext _operationOrderContext;
        private readonly IOperationOrderService _operationOrderService;

        public OperationOrderController(IOperationOrderService operationOrderService, IOperationOrderContext operationOrderContext)
        {
            _operationOrderService = operationOrderService;
            _operationOrderContext = operationOrderContext;
        }

        [HttpGet, Authorize]
        public IEnumerable<Opord> Get()
        {
            return _operationOrderContext.Get();
        }

        [HttpGet("{id}"), Authorize]
        public Opord Get(string id)
        {
            return _operationOrderContext.GetSingle(id);
        }

        [HttpPost, Authorize]
        public async Task Post([FromBody] CreateOperationOrderRequest request)
        {
            await _operationOrderService.Add(request);
        }

        [HttpPut, Authorize]
        public async Task Put([FromBody] Opord request)
        {
            await _operationOrderContext.Replace(request);
        }
    }
}
