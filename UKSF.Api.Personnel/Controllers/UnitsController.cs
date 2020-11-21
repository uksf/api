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
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class UnitsController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IEventBus<Account> _accountEventBus;
        private readonly IAssignmentService _assignmentService;
        private readonly IDisplayNameService _displayNameService;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly INotificationsService _notificationsService;
        private readonly IRanksService _ranksService;
        private readonly IRolesService _rolesService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;

        public UnitsController(
            IAccountContext accountContext,
            IUnitsContext unitsContext,
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
            _accountContext = accountContext;
            _unitsContext = unitsContext;
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
                    "auxiliary" => _unitsService.GetSortedUnits(x => x.Branch == UnitBranch.AUXILIARY && x.Members.Contains(accountId)),
                    "available" => _unitsService.GetSortedUnits(x => !x.Members.Contains(accountId)),
                    _           => _unitsService.GetSortedUnits(x => x.Members.Contains(accountId))
                };
                return Ok(response);
            }

            return Ok(_unitsService.GetSortedUnits());
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult GetSingle([FromRoute] string id) {
            Unit unit = _unitsContext.GetSingle(id);
            Unit parent = _unitsService.GetParent(unit);
            // TODO: Use a factory or mapper
            ResponseUnit response = _mapper.Map<ResponseUnit>(unit);
            response.Code = _unitsService.GetChainString(unit);
            response.ParentName = parent?.Name;
            response.UnitMembers = MapUnitMembers(unit);
            return Ok(response);
        }

        [HttpGet("exists/{check}"), Authorize]
        public IActionResult GetUnitExists([FromRoute] string check, [FromQuery] string id = "") {
            if (string.IsNullOrEmpty(check)) Ok(false);

            bool exists = _unitsContext.GetSingle(
                              x => (string.IsNullOrEmpty(id) || x.Id != id) &&
                                   (string.Equals(x.Name, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.Shortname, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.TeamspeakGroup, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.DiscordRoleId, check, StringComparison.InvariantCultureIgnoreCase) ||
                                    string.Equals(x.Callsign, check, StringComparison.InvariantCultureIgnoreCase))
                          ) !=
                          null;
            return Ok(exists);
        }

        [HttpGet("tree"), Authorize]
        public IActionResult GetTree() {
            Unit combatRoot = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.COMBAT);
            Unit auxiliaryRoot = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.AUXILIARY);
            ResponseUnitTreeDataSet dataSet = new() {
                CombatNodes = new List<ResponseUnitTree> { new() { Id = combatRoot.Id, Name = combatRoot.Name, Children = GetUnitTreeChildren(combatRoot) } },
                AuxiliaryNodes = new List<ResponseUnitTree> { new() { Id = auxiliaryRoot.Id, Name = auxiliaryRoot.Name, Children = GetUnitTreeChildren(auxiliaryRoot) } }
            };
            return Ok(dataSet);
        }

        // TODO: Use a mapper
        private IEnumerable<ResponseUnitTree> GetUnitTreeChildren(MongoObject parentUnit) {
            return _unitsContext.Get(x => x.Parent == parentUnit.Id).Select(unit => new ResponseUnitTree { Id = unit.Id, Name = unit.Name, Children = GetUnitTreeChildren(unit) });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> AddUnit([FromBody] Unit unit) {
            await _unitsContext.Add(unit);
            _logger.LogAudit($"New unit added: '{unit}'");
            return Ok();
        }

        [HttpPut("{id}"), Authorize]
        public async Task<IActionResult> EditUnit([FromRoute] string id, [FromBody] Unit unit) {
            Unit oldUnit = _unitsContext.GetSingle(x => x.Id == id);
            await _unitsContext.Replace(unit);
            _logger.LogAudit($"Unit '{unit.Shortname}' updated: {oldUnit.Changes(unit)}");

            // TODO: Move this elsewhere
            unit = _unitsContext.GetSingle(unit.Id);
            if (unit.Name != oldUnit.Name) {
                foreach (Account account in _accountContext.Get(x => x.UnitAssignment == oldUnit.Name)) {
                    await _accountContext.Update(account.Id, "unitAssignment", unit.Name);
                    _accountEventBus.Send(account);
                }
            }

            if (unit.TeamspeakGroup != oldUnit.TeamspeakGroup) {
                foreach (Account account in unit.Members.Select(x => _accountContext.GetSingle(x))) {
                    _accountEventBus.Send(account);
                }
            }

            if (unit.DiscordRoleId != oldUnit.DiscordRoleId) {
                foreach (Account account in unit.Members.Select(x => _accountContext.GetSingle(x))) {
                    _accountEventBus.Send(account);
                }
            }

            return Ok();
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteUnit([FromRoute] string id) {
            Unit unit = _unitsContext.GetSingle(id);
            _logger.LogAudit($"Unit deleted '{unit.Name}'");
            foreach (Account account in _accountContext.Get(x => x.UnitAssignment == unit.Name)) {
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(account.Id, "Reserves", reason: $"{unit.Name} was deleted");
                _notificationsService.Add(notification);
            }

            await _unitsContext.Delete(id);
            return Ok();
        }

        [HttpPatch("{id}/parent"), Authorize]
        public async Task<IActionResult> UpdateParent([FromRoute] string id, [FromBody] RequestUnitUpdateParent unitUpdate) {
            Unit unit = _unitsContext.GetSingle(id);
            Unit parentUnit = _unitsContext.GetSingle(unitUpdate.ParentId);
            if (unit.Parent == parentUnit.Id) return Ok();

            await _unitsContext.Update(id, "parent", parentUnit.Id);
            if (unit.Branch != parentUnit.Branch) {
                await _unitsContext.Update(id, "branch", parentUnit.Branch);
            }

            List<Unit> parentChildren = _unitsContext.Get(x => x.Parent == parentUnit.Id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.Id == unit.Id));
            parentChildren.Insert(unitUpdate.Index, unit);
            foreach (Unit child in parentChildren) {
                await _unitsContext.Update(child.Id, "order", parentChildren.IndexOf(child));
            }

            unit = _unitsContext.GetSingle(unit.Id);
            foreach (Unit child in _unitsService.GetAllChildren(unit, true)) {
                foreach (string accountId in child.Members) {
                    await _assignmentService.UpdateGroupsAndRoles(accountId);
                }
            }

            return Ok();
        }

        [HttpPatch("{id}/order"), Authorize]
        public IActionResult UpdateSortOrder([FromRoute] string id, [FromBody] RequestUnitUpdateOrder unitUpdate) {
            Unit unit = _unitsContext.GetSingle(id);
            Unit parentUnit = _unitsContext.GetSingle(x => x.Id == unit.Parent);
            List<Unit> parentChildren = _unitsContext.Get(x => x.Parent == parentUnit.Id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.Id == unit.Id));
            parentChildren.Insert(unitUpdate.Index, unit);
            foreach (Unit child in parentChildren) {
                _unitsContext.Update(child.Id, "order", parentChildren.IndexOf(child));
            }

            return Ok();
        }

        // TODO: Use mappers/factories
        [HttpGet("chart/{type}"), Authorize]
        public IActionResult GetUnitsChart([FromRoute] string type) {
            switch (type) {
                case "combat":
                    Unit combatRoot = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.COMBAT);
                    return Ok(
                        new ResponseUnitChartNode {
                            Id = combatRoot.Id,
                            Name = combatRoot.PreferShortname ? combatRoot.Shortname : combatRoot.Name,
                            Members = MapUnitMembers(combatRoot),
                            Children = GetUnitChartChildren(combatRoot.Id)
                        }
                    );
                case "auxiliary":
                    Unit auxiliaryRoot = _unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.AUXILIARY);
                    return Ok(
                        new ResponseUnitChartNode {
                            Id = auxiliaryRoot.Id,
                            Name = auxiliaryRoot.PreferShortname ? auxiliaryRoot.Shortname : auxiliaryRoot.Name,
                            Members = MapUnitMembers(auxiliaryRoot),
                            Children = GetUnitChartChildren(auxiliaryRoot.Id)
                        }
                    );
                default: return Ok();
            }
        }

        private IEnumerable<ResponseUnitChartNode> GetUnitChartChildren(string parent) {
            return _unitsContext.Get(x => x.Parent == parent)
                                .Select(
                                    unit => new ResponseUnitChartNode {
                                        Id = unit.Id, Name = unit.PreferShortname ? unit.Shortname : unit.Name, Members = MapUnitMembers(unit), Children = GetUnitChartChildren(unit.Id)
                                    }
                                );
        }

        private IEnumerable<ResponseUnitMember> MapUnitMembers(Unit unit) {
            return SortMembers(unit.Members, unit).Select(x => MapUnitMember(x, unit));
        }

        private ResponseUnitMember MapUnitMember(Account member, Unit unit) =>
            new() { Name = _displayNameService.GetDisplayName(member), Role = member.RoleAssignment, UnitRole = GetRole(unit, member.Id) };

        // TODO: Move somewhere else
        private IEnumerable<Account> SortMembers(IEnumerable<string> members, Unit unit) {
            return members.Select(
                              x => {
                                  Account account = _accountContext.GetSingle(x);
                                  return new { account, rankIndex = _ranksService.GetRankOrder(account.Rank), roleIndex = _unitsService.GetMemberRoleOrder(account, unit) };
                              }
                          )
                          .OrderByDescending(x => x.roleIndex)
                          .ThenBy(x => x.rankIndex)
                          .ThenBy(x => x.account.Lastname)
                          .ThenBy(x => x.account.Firstname)
                          .Select(x => x.account);
        }

        private string GetRole(Unit unit, string accountId) =>
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(0).Name) ? "1" :
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(1).Name) ? "2" :
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(2).Name) ? "3" :
            _unitsService.MemberHasRole(accountId, unit, _rolesService.GetUnitRoleByOrder(3).Name) ? "N" : "";
    }
}
