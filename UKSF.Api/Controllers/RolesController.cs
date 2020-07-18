using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers {
    [Route("[controller]")]
    public class RolesController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly INotificationsService notificationsService;
        private readonly IRolesService rolesService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public RolesController(IRolesService rolesService, IAccountService accountService, IAssignmentService assignmentService, ISessionService sessionService, IUnitsService unitsService, INotificationsService notificationsService) {
            this.rolesService = rolesService;
            this.accountService = accountService;
            this.assignmentService = assignmentService;
            this.sessionService = sessionService;
            this.unitsService = unitsService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IActionResult GetRoles([FromQuery] string id = "", [FromQuery] string unitId = "") {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(unitId)) {
                Unit unit = unitsService.Data.GetSingle(unitId);
                IOrderedEnumerable<Role> unitRoles = rolesService.Data.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order);
                IEnumerable<KeyValuePair<string, string>> existingPairs = unit.roles.Where(x => x.Value == id);
                IEnumerable<Role> filteredRoles = unitRoles.Where(x => existingPairs.All(y => y.Key != x.name));
                return Ok(filteredRoles);
            }

            if (!string.IsNullOrEmpty(id)) {
                Account account = accountService.Data.GetSingle(id);
                return Ok(rolesService.Data.Get(x => x.roleType == RoleType.INDIVIDUAL && x.name != account.roleAssignment).OrderBy(x => x.order));
            }

            return Ok(new {individualRoles = rolesService.Data.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Data.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpPost("{roleType}/{check}"), Authorize]
        public IActionResult CheckRole(RoleType roleType, string check, [FromBody] Role role = null) {
            if (string.IsNullOrEmpty(check)) return Ok();
            if (role != null) {
                Role safeRole = role;
                return Ok(rolesService.Data.GetSingle(x => x.id != safeRole.id && x.roleType == roleType && x.name == check));
            }

            return Ok(rolesService.Data.GetSingle(x => x.roleType == roleType && x.name == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddRole([FromBody] Role role) {
            await rolesService.Data.Add(role);
            LogWrapper.AuditLog($"Role added '{role.name}'");
            return Ok(new {individualRoles = rolesService.Data.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Data.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditRole([FromBody] Role role) {
            Role oldRole = rolesService.Data.GetSingle(x => x.id == role.id);
            LogWrapper.AuditLog($"Role updated from '{oldRole.name}' to '{role.name}'");
            await rolesService.Data.Update(role.id, "name", role.name);
            foreach (Account account in accountService.Data.Get(x => x.roleAssignment == oldRole.name)) {
                await accountService.Data.Update(account.id, "roleAssignment", role.name);
            }

            await unitsService.RenameRole(oldRole.name, role.name);
            return Ok(new {individualRoles = rolesService.Data.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Data.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteRole(string id) {
            Role role = rolesService.Data.GetSingle(x => x.id == id);
            LogWrapper.AuditLog($"Role deleted '{role.name}'");
            await rolesService.Data.Delete(id);
            foreach (Account account in accountService.Data.Get(x => x.roleAssignment == role.name)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, role: AssignmentService.REMOVE_FLAG, reason: $"the '{role.name}' role was deleted");
                notificationsService.Add(notification);
            }

            await unitsService.DeleteRole(role.name);
            return Ok(new {individualRoles = rolesService.Data.Get(x => x.roleType == RoleType.INDIVIDUAL), unitRoles = rolesService.Data.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order)});
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<Role> newRoleOrder) {
            for (int index = 0; index < newRoleOrder.Count; index++) {
                Role role = newRoleOrder[index];
                if (rolesService.Data.GetSingle(role.name).order != index) {
                    await rolesService.Data.Update(role.id, "order", index);
                }
            }

            return Ok(rolesService.Data.Get(x => x.roleType == RoleType.UNIT).OrderBy(x => x.order));
        }
    }
}
