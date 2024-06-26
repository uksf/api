﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Controllers;

[Route("variables")]
[Permissions(Permissions.Admin)]
public class VariablesController : ControllerBase
{
    private readonly IUksfLogger _logger;
    private readonly IVariablesContext _variablesContext;

    public VariablesController(IVariablesContext variablesContext, IUksfLogger logger)
    {
        _variablesContext = variablesContext;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public IEnumerable<VariableItem> GetAll()
    {
        return _variablesContext.Get();
    }

    [HttpGet("{key}")]
    [Authorize]
    public VariableItem GetVariableItem([FromRoute] string key)
    {
        return _variablesContext.GetSingle(key);
    }

    [HttpPost("{key}")]
    [Authorize]
    public VariableItem CheckVariableItem([FromRoute] string key, [FromBody] VariableItem variableItem = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new BadRequestException("No key given");
        }

        if (variableItem != null)
        {
            var safeVariableItem = variableItem;
            return _variablesContext.GetSingle(x => x.Id != safeVariableItem.Id && x.Key == key.Keyify());
        }

        return _variablesContext.GetSingle(x => x.Key == key.Keyify());
    }

    [HttpPut]
    [Authorize]
    public async Task AddVariableItem([FromBody] VariableItem variableItem)
    {
        variableItem.Key = variableItem.Key.Keyify();
        await _variablesContext.Add(variableItem);
        _logger.LogAudit($"VariableItem added '{variableItem.Key}, {variableItem.Item}'");
    }

    [HttpPatch]
    [Authorize]
    public async Task<IEnumerable<VariableItem>> EditVariableItem([FromBody] VariableItem variableItem)
    {
        var oldVariableItem = _variablesContext.GetSingle(variableItem.Key);
        _logger.LogAudit($"VariableItem '{oldVariableItem.Key}' updated from '{oldVariableItem.Item}' to '{variableItem.Item}'");
        await _variablesContext.Update(variableItem.Key, variableItem.Item);
        return _variablesContext.Get();
    }

    [HttpDelete("{key}")]
    [Authorize]
    public async Task<IEnumerable<VariableItem>> DeleteVariableItem([FromRoute] string key)
    {
        var variableItem = _variablesContext.GetSingle(key);
        _logger.LogAudit($"VariableItem deleted '{variableItem.Key}, {variableItem.Item}'");
        await _variablesContext.Delete(key);
        return _variablesContext.Get();
    }
}
