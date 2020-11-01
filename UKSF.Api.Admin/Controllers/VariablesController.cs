using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Base;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;

namespace UKSF.Api.Admin.Controllers {
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class VariablesController : Controller {
        private readonly IVariablesDataService variablesDataService;
        private readonly ILogger logger;

        public VariablesController(IVariablesDataService variablesDataService, ILogger logger) {
            this.variablesDataService = variablesDataService;
            this.logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult GetAll() => Ok(variablesDataService.Get());

        [HttpGet("{key}"), Authorize]
        public IActionResult GetVariableItems(string key) => Ok(variablesDataService.GetSingle(key));

        [HttpPost("{key}"), Authorize]
        public IActionResult CheckVariableItem(string key, [FromBody] VariableItem variableItem = null) {
            if (string.IsNullOrEmpty(key)) return Ok();
            if (variableItem != null) {
                VariableItem safeVariableItem = variableItem;
                return Ok(variablesDataService.GetSingle(x => x.id != safeVariableItem.id && x.key == key.Keyify()));
            }

            return Ok(variablesDataService.GetSingle(x => x.key == key.Keyify()));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddVariableItem([FromBody] VariableItem variableItem) {
            variableItem.key = variableItem.key.Keyify();
            await variablesDataService.Add(variableItem);
            logger.LogAudit($"VariableItem added '{variableItem.key}, {variableItem.item}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditVariableItem([FromBody] VariableItem variableItem) {
            VariableItem oldVariableItem = variablesDataService.GetSingle(variableItem.key);
            logger.LogAudit($"VariableItem '{oldVariableItem.key}' updated from '{oldVariableItem.item}' to '{variableItem.item}'");
            await variablesDataService.Update(variableItem.key, variableItem.item);
            return Ok(variablesDataService.Get());
        }

        [HttpDelete("{key}"), Authorize]
        public async Task<IActionResult> DeleteVariableItem(string key) {
            VariableItem variableItem = variablesDataService.GetSingle(key);
            logger.LogAudit($"VariableItem deleted '{variableItem.key}, {variableItem.item}'");
            await variablesDataService.Delete(key);
            return Ok(variablesDataService.Get());
        }
    }
}
