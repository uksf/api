﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;

namespace UKSF.Api.Command.Controllers {
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class OperationOrderController : Controller {
        private readonly IOperationOrderService operationOrderService;

        public OperationOrderController(IOperationOrderService operationOrderService) => this.operationOrderService = operationOrderService;

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(operationOrderService.Data.Get());

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) => Ok(new {result = operationOrderService.Data.GetSingle(id)});

        [HttpPost, Authorize]
        public async Task<IActionResult> Post([FromBody] CreateOperationOrderRequest request) {
            await operationOrderService.Add(request);
            return Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> Put([FromBody] Opord request) {
            await operationOrderService.Data.Replace(request);
            return Ok();
        }
    }
}