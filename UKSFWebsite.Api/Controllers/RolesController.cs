using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class RolesController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly IRolesService rolesService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public RolesController(IRolesService rolesService, IAccountService accountService, IAssignmentService assignmentService, ISessionService sessionService, IUnitsService unitsService) {
            this.rolesService = rolesService;
            this.accountService = accountService;
            this.assignmentService = assignmentService;
            this.sessionService = sessionService;
            this.unitsService = unitsService;
        }

        [HttpGet, Authorize]
        public IActionResult GetRoles([FromQuery] string id = "", [FromQuery] string unitId = "") {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(unitId)) {
                Unit unit = unitsService.GetSingle(unitId);
                IOrderedEnumerable<Role> unitRoles = rolesService.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order);
                IEnumerable<KeyValuePair<string, string>> existingPairs = unit.roles.Where(x => x.Value == id);
                IEnumerable<Role> filteredRoles = unitRoles.Where(x => existingPairs.All(y => y.Key != x.name));
                return Ok(filteredRoles);
            }

            if (!string.IsNullOrEmpty(id)) {
                Account account = accountService.GetSingle(id);
                return Ok(rolesService.Get(x => x.roleType == RoleType.INDIVIDUAL && x.name != account.roleAssignment).OrderBy(x => x.order));
            }
            return Ok(new {individualRoles = rolesService.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpPost("{roleType}/{check}"), Authorize]
        public IActionResult CheckRole(RoleType roleType, string check, [FromBody] Role role = null) {
            if (string.IsNullOrEmpty(check)) return Ok();
            return Ok(role != null ? rolesService.GetSingle(x => x.id != role.id && x.roleType == roleType && x.name == check) : rolesService.GetSingle(x => x.roleType == roleType && x.name == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddRole([FromBody] Role role) {
            await rolesService.Add(role);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Role added '{role.name}'");
            return Ok(new {individualRoles = rolesService.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditRole([FromBody] Role role) {
            Role oldRole = rolesService.GetSingle(x => x.id == role.id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Role updated from '{oldRole.name}' to '{role.name}'");
            await rolesService.Update(role.id, "name", role.name);
            foreach (Account account in accountService.Get(x => x.roleAssignment == oldRole.name)) {
                await accountService.Update(account.id, "roleAssignment", role.name);
            }
            
            await unitsService.RenameRole(oldRole.name, role.name);
            return Ok(new {individualRoles = rolesService.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteRole(string id) {
            Role role = rolesService.GetSingle(x => x.id == id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Role deleted '{role.name}'");
            await rolesService.Delete(id);
            foreach (Account account in accountService.Get(x => x.roleAssignment == role.name)) {
                await assignmentService.UpdateUnitRankAndRole(account.id, role: AssignmentService.REMOVE_FLAG, reason: $"the '{role.name}' role was deleted");
            }

            await unitsService.DeleteRole(role.name);
            return Ok(new {individualRoles = rolesService.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<Role> newRoleOrder) {
            for (int index = 0; index < newRoleOrder.Count; index++) {
                Role role = newRoleOrder[index];
                if (rolesService.GetSingle(role.name).order != index) {
                    await rolesService.Update(role.id, "order", index);
                }
            }

            return Ok(rolesService.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order));
        }
    }
}
