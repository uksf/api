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
public class OperationOrderController(IOperationOrderService operationOrderService, IOperationOrderContext operationOrderContext) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainOpord> Get()
    {
        return operationOrderContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public DomainOpord Get([FromRoute] string id)
    {
        return operationOrderContext.GetSingle(id);
    }

    [HttpPost]
    [Authorize]
    public async Task Post([FromBody] CreateOperationOrderRequest request)
    {
        await operationOrderService.Add(request);
    }

    [HttpPut]
    [Authorize]
    public async Task Put([FromBody] DomainOpord request)
    {
        await operationOrderContext.Replace(request);
    }
}
