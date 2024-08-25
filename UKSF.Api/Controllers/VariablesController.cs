using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Controllers;

[Route("variables")]
[Permissions(Permissions.Admin)]
public class VariablesController(IVariablesContext variablesContext, IUksfLogger logger) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainVariableItem> GetAll()
    {
        return variablesContext.Get();
    }

    [HttpGet("{key}")]
    [Authorize]
    public DomainVariableItem GetVariableItem([FromRoute] string key)
    {
        return variablesContext.GetSingle(key);
    }

    [HttpPost("{key}")]
    [Authorize]
    public DomainVariableItem CheckVariableItem([FromRoute] string key, [FromBody] DomainVariableItem variableItem = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new BadRequestException("No key given");
        }

        if (variableItem is not null)
        {
            var safeVariableItem = variableItem;
            return variablesContext.GetSingle(x => x.Id != safeVariableItem.Id && x.Key == key.Keyify());
        }

        return variablesContext.GetSingle(x => x.Key == key.Keyify());
    }

    [HttpPut]
    [Authorize]
    public async Task AddVariableItem([FromBody] DomainVariableItem variableItem)
    {
        variableItem.Key = variableItem.Key.Keyify();
        await variablesContext.Add(variableItem);
        logger.LogAudit($"VariableItem added '{variableItem.Key}, {variableItem.Item}'");
    }

    [HttpPatch]
    [Authorize]
    public async Task<IEnumerable<DomainVariableItem>> EditVariableItem([FromBody] DomainVariableItem variableItem)
    {
        var oldVariableItem = variablesContext.GetSingle(variableItem.Key);
        logger.LogAudit($"VariableItem '{oldVariableItem.Key}' updated from '{oldVariableItem.Item}' to '{variableItem.Item}'");
        await variablesContext.Update(variableItem.Key, variableItem.Item);
        return variablesContext.Get();
    }

    [HttpDelete("{key}")]
    [Authorize]
    public async Task<IEnumerable<DomainVariableItem>> DeleteVariableItem([FromRoute] string key)
    {
        var variableItem = variablesContext.GetSingle(key);
        logger.LogAudit($"VariableItem deleted '{variableItem.Key}, {variableItem.Item}'");
        await variablesContext.Delete(key);
        return variablesContext.Get();
    }
}
