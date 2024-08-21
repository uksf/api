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
public class OperationOrderController : ControllerBase
{
    private readonly IOperationOrderContext _operationOrderContext;
    private readonly IOperationOrderService _operationOrderService;

    public OperationOrderController(IOperationOrderService operationOrderService, IOperationOrderContext operationOrderContext)
    {
        _operationOrderService = operationOrderService;
        _operationOrderContext = operationOrderContext;
    }

    [HttpGet]
    [Authorize]
    public IEnumerable<DomainOpord> Get()
    {
        return _operationOrderContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public DomainOpord Get([FromRoute] string id)
    {
        return _operationOrderContext.GetSingle(id);
    }

    [HttpPost]
    [Authorize]
    public async Task Post([FromBody] CreateOperationOrderRequest request)
    {
        await _operationOrderService.Add(request);
    }

    [HttpPut]
    [Authorize]
    public async Task Put([FromBody] DomainOpord request)
    {
        await _operationOrderContext.Replace(request);
    }
}
