using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.ADMIN)]
    public class VariablesController : Controller {
        private readonly ISessionService sessionService;
        private readonly IVariablesService variablesService;

        public VariablesController(IVariablesService variablesService, ISessionService sessionService) {
            this.variablesService = variablesService;
            this.sessionService = sessionService;
        }

        [HttpGet, Authorize]
        public IActionResult GetAll() => Ok(variablesService.Get());

        [HttpGet("{key}"), Authorize]
        public IActionResult GetVariableItems(string key) => Ok(variablesService.GetSingle(key));

        [HttpPost("{key}"), Authorize]
        public IActionResult CheckVariableItem(string key, [FromBody] VariableItem variableItem = null) {
            if (string.IsNullOrEmpty(key)) return Ok();
            return Ok(variableItem != null ? variablesService.GetSingle(x => x.id != variableItem.id && x.key == key.Keyify()) : variablesService.GetSingle(x => x.key == key.Keyify()));
        }

        [HttpPost, Authorize]
        public IActionResult CheckVariableItem([FromBody] VariableItem variableItem) {
            return variableItem != null ? (IActionResult) Ok(variablesService.GetSingle(x => x.id != variableItem.id && x.key == variableItem.key.Keyify())) : Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddVariableItem([FromBody] VariableItem variableItem) {
            variableItem.key = variableItem.key.Keyify();
            await variablesService.Add(variableItem);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"VariableItem added '{variableItem.key}, {variableItem.item}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditVariableItem([FromBody] VariableItem variableItem) {
            VariableItem oldVariableItem = variablesService.GetSingle(variableItem.key);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"VariableItem '{oldVariableItem.key}' updated from '{oldVariableItem.item}' to '{variableItem.item}'");
            await variablesService.Update(variableItem.key, variableItem.item);
            return Ok(variablesService.Get());
        }

        [HttpDelete("{key}"), Authorize]
        public async Task<IActionResult> DeleteVariableItem(string key) {
            VariableItem variableItem = variablesService.GetSingle(key);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"VariableItem deleted '{variableItem.key}, {variableItem.item}'");
            await variablesService.Delete(key);
            return Ok(variablesService.Get());
        }
    }
}
