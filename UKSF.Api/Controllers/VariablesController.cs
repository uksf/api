using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Admin;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Common;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.ADMIN)]
    public class VariablesController : Controller {
        private readonly IVariablesDataService variablesDataService;

        public VariablesController(IVariablesDataService variablesDataService) => this.variablesDataService = variablesDataService;

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
            LogWrapper.AuditLog($"VariableItem added '{variableItem.key}, {variableItem.item}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditVariableItem([FromBody] VariableItem variableItem) {
            VariableItem oldVariableItem = variablesDataService.GetSingle(variableItem.key);
            LogWrapper.AuditLog($"VariableItem '{oldVariableItem.key}' updated from '{oldVariableItem.item}' to '{variableItem.item}'");
            await variablesDataService.Update(variableItem.key, variableItem.item);
            return Ok(variablesDataService.Get());
        }

        [HttpDelete("{key}"), Authorize]
        public async Task<IActionResult> DeleteVariableItem(string key) {
            VariableItem variableItem = variablesDataService.GetSingle(key);
            LogWrapper.AuditLog($"VariableItem deleted '{variableItem.key}, {variableItem.item}'");
            await variablesDataService.Delete(key);
            return Ok(variablesDataService.Get());
        }
    }
}
