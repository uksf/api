using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Admin.Controllers
{
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class VariablesController : Controller
    {
        private readonly ILogger _logger;
        private readonly IVariablesContext _variablesContext;

        public VariablesController(IVariablesContext variablesContext, ILogger logger)
        {
            _variablesContext = variablesContext;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult GetAll()
        {
            return Ok(_variablesContext.Get());
        }

        [HttpGet("{key}"), Authorize]
        public IActionResult GetVariableItems(string key)
        {
            return Ok(_variablesContext.GetSingle(key));
        }

        [HttpPost("{key}"), Authorize]
        public IActionResult CheckVariableItem(string key, [FromBody] VariableItem variableItem = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                return Ok();
            }

            if (variableItem != null)
            {
                VariableItem safeVariableItem = variableItem;
                return Ok(_variablesContext.GetSingle(x => x.Id != safeVariableItem.Id && x.Key == key.Keyify()));
            }

            return Ok(_variablesContext.GetSingle(x => x.Key == key.Keyify()));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddVariableItem([FromBody] VariableItem variableItem)
        {
            variableItem.Key = variableItem.Key.Keyify();
            await _variablesContext.Add(variableItem);
            _logger.LogAudit($"VariableItem added '{variableItem.Key}, {variableItem.Item}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditVariableItem([FromBody] VariableItem variableItem)
        {
            VariableItem oldVariableItem = _variablesContext.GetSingle(variableItem.Key);
            _logger.LogAudit($"VariableItem '{oldVariableItem.Key}' updated from '{oldVariableItem.Item}' to '{variableItem.Item}'");
            await _variablesContext.Update(variableItem.Key, variableItem.Item);
            return Ok(_variablesContext.Get());
        }

        [HttpDelete("{key}"), Authorize]
        public async Task<IActionResult> DeleteVariableItem(string key)
        {
            VariableItem variableItem = _variablesContext.GetSingle(key);
            _logger.LogAudit($"VariableItem deleted '{variableItem.Key}, {variableItem.Item}'");
            await _variablesContext.Delete(key);
            return Ok(_variablesContext.Get());
        }
    }
}
