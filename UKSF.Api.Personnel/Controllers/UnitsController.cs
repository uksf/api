using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class UnitsController : Controller {
        private readonly IEventBus<Account> _accountEventBus;
        private readonly IAccountService _accountService;
        private readonly IAssignmentService _assignmentService;
        private readonly IDisplayNameService _displayNameService;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly INotificationsService _notificationsService;
        private readonly IRanksService _ranksService;
        private readonly IRolesService _rolesService;
        private readonly IUnitsService _unitsService;

        public UnitsController(
            IAccountService accountService,
            IDisplayNameService displayNameService,
            IRanksService ranksService,
            IUnitsService unitsService,
            IRolesService rolesService,
            IAssignmentService assignmentService,
            INotificationsService notificationsService,
            IEventBus<Account> accountEventBus,
            IMapper mapper,
            ILogger logger
        ) {
            _accountService = accountService;
            _displayNameService = displayNameService;
            _ranksService = ranksService;
            _unitsService = unitsService;
            _rolesService = rolesService;
            _assignmentService = assignmentService;
            _notificationsService = notificationsService;
            _accountEventBus = accountEventBus;
            _mapper = mapper;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult Get([FromQuery] string filter = "", [FromQuery] string accountId = "") {
            if (!string.IsNullOrEmpty(accountId)) {
                IEnumerable<Unit> response = filter switch {
                    "auxiliary" => _unitsService.GetSortedUnits(x => x.branch == UnitBranch.AUXILIARY && x.members.Contains(accountId)),
                    "available" => _unitsService.GetSortedUnits(x => !x.members.Contains(accountId)),
                    _           => _unitsService.GetSortedUnits(x => x.members.Contains(accountId))
                };
                return Ok(response);
            }

            return Ok(_unitsService.GetSortedUnits());
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult GetSingle([FromRoute] string id) {
            Unit unit = _unitsService.Data.GetSingle(id);
            Unit parent = _unitsService.GetParent(unit);
            // TODO: Use a factory or mapper
            ResponseUnit response = _mapper.Map<ResponseUnit>(unit);
            response.code = _unitsService.GetChainString(unit);
            response.parentName = parent?.name;
            response.unitMembers = MapUnitMembers(unit);
            return Ok(response);
        }

        [HttpGet("exists/{check}"), Authorize]
        public IActionResult GetUnitExists([FromRoute] string check, [FromQuery] string id = "") {
            if (string.IsNullOrEmpty(check)) Ok(false);

            bool exists = _unitsService.Data.GetSingle(
                              x => (string.IsNullOrEmpty(id) || x.id != id) &&
                                   (string.Equals(x.name, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.shortname, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.teamspeakGroup, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.discordRoleId, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.callsign, check, StringComparison.InvariantCultureIgnoreCase))
                          ) !=
                          null;
            return Ok(exists);
        }

        [HttpGet("tree"), Authorize]
        public IActionResult GetTree() {
            Unit combatRoot = _unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
            Unit auxiliaryRoot = _unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
            ResponseUnitTreeDataSet dataSet = new ResponseUnitTreeDataSet {
                combatNodes = new List<ResponseUnitTree> { new ResponseUnitTree { id = combatRoot.id, name = combatRoot.name, children = GetUnitTreeChildren(combatRoot) } },
                auxiliaryNodes = new List<ResponseUnitTree> { new ResponseUnitTree { id = auxiliaryRoot.id, name = auxiliaryRoot.name, children = GetUnitTreeChildren(auxiliaryRoot) } }
            };
            return Ok(dataSet);
        }

        // TODO: Use a mapper
        private IEnumerable<ResponseUnitTree> GetUnitTreeChildren(DatabaseObject parentUnit) {
            return _unitsService.Data.Get(x => x.parent == parentUnit.id).Select(unit => new ResponseUnitTree { id = unit.id, name = unit.name, children = GetUnitTreeChildren(unit) });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> AddUnit([FromBody] Unit unit) {
            await _unitsService.Data.Add(unit);
            _logger.LogAudit($"New unit added: '{unit}'");
            return Ok();
        }

        [HttpPut("{id}"), Authorize]
        public async Task<IActionResult> EditUnit([FromRoute] string id, [FromBody] Unit unit) {
            Unit oldUnit = _unitsService.Data.GetSingle(x => x.id == id);
            await _unitsService.Data.Replace(unit);
            _logger.LogAudit($"Unit '{unit.shortname}' updated: {oldUnit.Changes(unit)}");

            // TODO: Move this elsewhere
            unit = _unitsService.Data.GetSingle(unit.id);
            if (unit.name != oldUnit.name) {
                foreach (Account account in _accountService.Data.Get(x => x.unitAssignment == oldUnit.name)) {
                    await _accountService.Data.Update(account.id, "unitAssignment", unit.name);
                    _accountEventBus.Send(account);
                }
            }

            if (unit.teamspeakGroup != oldUnit.teamspeakGroup) {
                foreach (Account account in unit.members.Select(x => _accountService.Data.GetSingle(x))) {
                    _accountEventBus.Send(account);
                }
            }

            if (unit.discordRoleId != oldUnit.discordRoleId) {
                foreach (Account account in unit.members.Select(x => _accountService.Data.GetSingle(x))) {
                    _accountEventBus.Send(account);
                }
            }

            return Ok();
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteUnit([FromRoute] string id) {
            Unit unit = _unitsService.Data.GetSingle(id);
            _logger.LogAudit($"Unit deleted '{unit.name}'");
            foreach (Account account in _accountService.Data.Get(x => x.unitAssignment == unit.name)) {
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(account.id, "Reserves", reason: $"{unit.name} was deleted");
                _notificationsService.Add(notification);
            }

            await _unitsService.Data.Delete(id);
            return Ok();
        }

        [HttpPatch("{id}/parent"), Authorize]
        public async Task<IActionResult> UpdateParent([FromRoute] string id, [FromBody] RequestUnitUpdateParent unitUpdate) {
            Unit unit = _unitsService.Data.GetSingle(id);
            Unit parentUnit = _unitsService.Data.GetSingle(unitUpdate.parentId);
            if (unit.parent == parentUnit.id) return Ok();

            await _unitsService.Data.Update(id, "parent", parentUnit.id);
            if (unit.branch != parentUnit.branch) {
                await _unitsService.Data.Update(id, "branch", parentUnit.branch);
            }

            List<Unit> parentChildren = _unitsService.Data.Get(x => x.parent == parentUnit.id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.id == unit.id));
            parentChildren.Insert(unitUpdate.index, unit);
            foreach (Unit child in parentChildren) {
                await _unitsService.Data.Update(child.id, "order", parentChildren.IndexOf(child));
            }

            unit = _unitsService.Data.GetSingle(unit.id);
            foreach (Unit child in _unitsService.GetAllChildren(unit, true)) {
                foreach (string accountId in child.members) {
                    await _assignmentService.UpdateGroupsAndRoles(accountId);
                }
            }

            return Ok();
        }

        [HttpPatch("{id}/order"), Authorize]
        public IActionResult UpdateSortOrder([FromRoute] string id, [FromBody] RequestUnitUpdateOrder unitUpdate) {
            Unit unit = _unitsService.Data.GetSingle(id);
            Unit parentUnit = _unitsService.Data.GetSingle(x => x.id == unit.parent);
            List<Unit> parentChildren = _unitsService.Data.Get(x => x.parent == parentUnit.id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.id == unit.id));
            parentChildren.Insert(unitUpdate.index, unit);
            foreach (Unit child in parentChildren) {
                _unitsService.Data.Update(child.id, "order", parentChildren.IndexOf(child));
            }

            return Ok();
        }

        // TODO: Use mappers/factories
        [HttpGet("chart/{type}"), Authorize]
        public IActionResult GetUnitsChart([FromRoute] string type) {
            switch (type) {
                case "combat":
                    Unit combatRoot = _unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
                    return Ok(
                        new ResponseUnitChartNode {
                            id = combatRoot.id,
                            name = combatRoot.preferShortname ? combatRoot.shortname : combatRoot.name,
                            members = MapUnitMembers(combatRoot),
                            children = GetUnitChartChildren(combatRoot.id)
                        }
                    );
                case "auxiliary":
                    Unit auxiliaryRoot = _unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
                    return Ok(
                        new ResponseUnitChartNode {
                            id = auxiliaryRoot.id,
                            name = auxiliaryRoot.preferShortname ? auxiliaryRoot.shortname : auxiliaryRoot.name,
                            members = MapUnitMembers(auxiliaryRoot),
                            children = GetUnitChartChildren(auxiliaryRoot.id)
                        }
                    );
                default: return Ok();
            }
        }

        private IEnumerable<ResponseUnitChartNode> GetUnitChartChildren(string parent) {
            return _unitsService.Data.Get(x => x.parent == parent)
                                .Select(
                                    unit => new ResponseUnitChartNode {
                                        id = unit.id, name = unit.preferShortname ? unit.shortname : unit.name, members = MapUnitMembers(unit), children = GetUnitChartChildren(unit.id)
                                    }
                                );
        }

        private IEnumerable<ResponseUnitMember> MapUnitMembers(Unit unit) {
            return SortMembers(unit.members, unit).Select(x => MapUnitMember(x, unit));
        }

        private ResponseUnitMember MapUnitMember(Account member, Unit unit) =>
            new ResponseUnitMember { name = _displayNameService.GetDisplayName(member), role = member.roleAssignment, unitRole = GetRole(unit, member.id) };

        // TODO: Move somewhere else
        private IEnumerable<Account> SortMembers(IEnumerable<string> members, Unit unit) {
            return members.Select(
                              x => {
                                  Account account = _accountService.Data.GetSingle(x);
                                  return new { account, rankIndex = _ranksService.GetRankOrder(account.rank), roleIndex = _unitsService.GetMemberRoleOrder(account, unit) };
                              }
                          )
                          .OrderByDescending(x => x.roleIndex)
                          .ThenBy(x => x.rankIndex)
                          .ThenBy(x => x.account.lastname)
                          .ThenBy(x => x.account.firstname)
                          .Select(x => x.account);
        }

        private string GetRole(Unit unit, string accountId) =>
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(0).name) ? "1" :
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(1).name) ? "2" :
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(2).name) ? "3" :
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(3).name) ? "N" : "";
    }
}
