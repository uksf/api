using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class RolesController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IAssignmentService _assignmentService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IRolesContext _rolesContext;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;

        public RolesController(
            IUnitsContext unitsContext,
            IRolesContext rolesContext,
            IAccountContext accountContext,
            IAssignmentService assignmentService,
            IUnitsService unitsService,
            INotificationsService notificationsService,
            ILogger logger
        ) {
            _unitsContext = unitsContext;
            _rolesContext = rolesContext;
            _accountContext = accountContext;
            _assignmentService = assignmentService;
            _unitsService = unitsService;
            _notificationsService = notificationsService;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult GetRoles([FromQuery] string id = "", [FromQuery] string unitId = "") {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(unitId)) {
                Unit unit = _unitsContext.GetSingle(unitId);
                IOrderedEnumerable<Role> unitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order);
                IEnumerable<KeyValuePair<string, string>> existingPairs = unit.Roles.Where(x => x.Value == id);
                IEnumerable<Role> filteredRoles = unitRoles.Where(x => existingPairs.All(y => y.Key != x.Name));
                return Ok(filteredRoles);
            }

            if (!string.IsNullOrEmpty(id)) {
                Account account = _accountContext.GetSingle(id);
                return Ok(_rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL && x.Name != account.RoleAssignment).OrderBy(x => x.Order));
            }

            return Ok(new { individualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL), unitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order) });
        }

        [HttpPost("{roleType}/{check}"), Authorize]
        public IActionResult CheckRole(RoleType roleType, string check, [FromBody] Role role = null) {
            if (string.IsNullOrEmpty(check)) return Ok();
            if (role != null) {
                Role safeRole = role;
                return Ok(_rolesContext.GetSingle(x => x.Id != safeRole.Id && x.RoleType == roleType && x.Name == check));
            }

            return Ok(_rolesContext.GetSingle(x => x.RoleType == roleType && x.Name == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddRole([FromBody] Role role) {
            await _rolesContext.Add(role);
            _logger.LogAudit($"Role added '{role.Name}'");
            return Ok(new { individualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL), unitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order) });
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditRole([FromBody] Role role) {
            Role oldRole = _rolesContext.GetSingle(x => x.Id == role.Id);
            _logger.LogAudit($"Role updated from '{oldRole.Name}' to '{role.Name}'");
            await _rolesContext.Update(role.Id, "name", role.Name);
            foreach (Account account in _accountContext.Get(x => x.RoleAssignment == oldRole.Name)) {
                await _accountContext.Update(account.Id, "roleAssignment", role.Name);
            }

            await _unitsService.RenameRole(oldRole.Name, role.Name);
            return Ok(new { individualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL), unitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order) });
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteRole(string id) {
            Role role = _rolesContext.GetSingle(x => x.Id == id);
            _logger.LogAudit($"Role deleted '{role.Name}'");
            await _rolesContext.Delete(id);
            foreach (Account account in _accountContext.Get(x => x.RoleAssignment == role.Name)) {
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(account.Id, role: AssignmentService.REMOVE_FLAG, reason: $"the '{role.Name}' role was deleted");
                _notificationsService.Add(notification);
            }

            await _unitsService.DeleteRole(role.Name);
            return Ok(new { individualRoles = _rolesContext.Get(x => x.RoleType == RoleType.INDIVIDUAL), unitRoles = _rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order) });
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<Role> newRoleOrder) {
            for (int index = 0; index < newRoleOrder.Count; index++) {
                Role role = newRoleOrder[index];
                if (_rolesContext.GetSingle(role.Name).Order != index) {
                    await _rolesContext.Update(role.Id, "order", index);
                }
            }

            return Ok(_rolesContext.Get(x => x.RoleType == RoleType.UNIT).OrderBy(x => x.Order));
        }
    }
}
