using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class UnitsController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly IDiscordService discordService;
        private readonly IDisplayNameService displayNameService;
        private readonly IMapper mapper;
        private readonly ILogger logger;
        private readonly INotificationsService notificationsService;
        private readonly IRanksService ranksService;
        private readonly IRolesService rolesService;
        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;

        public UnitsController(
            IAccountService accountService,
            IDisplayNameService displayNameService,
            IRanksService ranksService,
            IUnitsService unitsService,
            IRolesService rolesService,
            ITeamspeakService teamspeakService,
            IAssignmentService assignmentService,
            IDiscordService discordService,
            INotificationsService notificationsService,
            IMapper mapper,
            ILogger logger
        ) {
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.rolesService = rolesService;
            this.teamspeakService = teamspeakService;
            this.assignmentService = assignmentService;
            this.discordService = discordService;
            this.notificationsService = notificationsService;
            this.mapper = mapper;
            this.logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult Get([FromQuery] string filter = "", [FromQuery] string accountId = "") {
            if (!string.IsNullOrEmpty(accountId)) {
                IEnumerable<Unit> response = filter switch {
                    "auxiliary" => unitsService.GetSortedUnits(x => x.branch == UnitBranch.AUXILIARY && x.members.Contains(accountId)),
                    "available" => unitsService.GetSortedUnits(x => !x.members.Contains(accountId)),
                    _           => unitsService.GetSortedUnits(x => x.members.Contains(accountId))
                };
                return Ok(response);
            }

            return Ok(unitsService.GetSortedUnits());
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult GetSingle([FromRoute] string id) {
            Unit unit = unitsService.Data.GetSingle(id);
            Unit parent = unitsService.GetParent(unit);
            // TODO: Use a factory or mapper
            ResponseUnit response = mapper.Map<ResponseUnit>(unit);
            response.code = unitsService.GetChainString(unit);
            response.parentName = parent?.name;
            response.unitMembers = MapUnitMembers(unit);
            return Ok(response);
        }

        [HttpGet("exists/{check}"), Authorize]
        public IActionResult GetUnitExists([FromRoute] string check, [FromQuery] string id = "") {
            if (string.IsNullOrEmpty(check)) Ok(false);

            bool exists = unitsService.Data.GetSingle(
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
            Unit combatRoot = unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
            Unit auxiliaryRoot = unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
            ResponseUnitTreeDataSet dataSet = new ResponseUnitTreeDataSet {
                combatNodes = new List<ResponseUnitTree> { new ResponseUnitTree { id = combatRoot.id, name = combatRoot.name, children = GetUnitTreeChildren(combatRoot) } },
                auxiliaryNodes = new List<ResponseUnitTree> { new ResponseUnitTree { id = auxiliaryRoot.id, name = auxiliaryRoot.name, children = GetUnitTreeChildren(auxiliaryRoot) } }
            };
            return Ok(dataSet);
        }

        // TODO: Use a mapper
        private IEnumerable<ResponseUnitTree> GetUnitTreeChildren(DatabaseObject parentUnit) {
            return unitsService.Data.Get(x => x.parent == parentUnit.id).Select(unit => new ResponseUnitTree { id = unit.id, name = unit.name, children = GetUnitTreeChildren(unit) });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> AddUnit([FromBody] Unit unit) {
            await unitsService.Data.Add(unit);
            logger.LogAudit($"New unit added: '{unit}'");
            return Ok();
        }

        [HttpPut("{id}"), Authorize]
        public async Task<IActionResult> EditUnit([FromRoute] string id, [FromBody] Unit unit) {
            Unit oldUnit = unitsService.Data.GetSingle(x => x.id == id);
            await unitsService.Data.Replace(unit);
            logger.LogAudit($"Unit '{unit.shortname}' updated: {oldUnit.Changes(unit)}");

            // TODO: Move this elsewhere
            unit = unitsService.Data.GetSingle(unit.id);
            if (unit.name != oldUnit.name) {
                foreach (Account account in accountService.Data.Get(x => x.unitAssignment == oldUnit.name)) {
                    await accountService.Data.Update(account.id, "unitAssignment", unit.name);
                    await teamspeakService.UpdateAccountTeamspeakGroups(accountService.Data.GetSingle(account.id));
                }
            }

            if (unit.teamspeakGroup != oldUnit.teamspeakGroup) {
                foreach (Account account in unit.members.Select(x => accountService.Data.GetSingle(x))) {
                    await teamspeakService.UpdateAccountTeamspeakGroups(account);
                }
            }

            if (unit.discordRoleId != oldUnit.discordRoleId) {
                foreach (Account account in unit.members.Select(x => accountService.Data.GetSingle(x))) {
                    await discordService.UpdateAccount(account);
                }
            }

            return Ok();
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteUnit([FromRoute] string id) {
            Unit unit = unitsService.Data.GetSingle(id);
            logger.LogAudit($"Unit deleted '{unit.name}'");
            foreach (Account account in accountService.Data.Get(x => x.unitAssignment == unit.name)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, "Reserves", reason: $"{unit.name} was deleted");
                notificationsService.Add(notification);
            }

            await unitsService.Data.Delete(id);
            return Ok();
        }

        [HttpPatch("{id}/parent"), Authorize]
        public async Task<IActionResult> UpdateParent([FromRoute] string id, [FromBody] RequestUnitUpdateParent unitUpdate) {
            Unit unit = unitsService.Data.GetSingle(id);
            Unit parentUnit = unitsService.Data.GetSingle(unitUpdate.parentId);
            if (unit.parent == parentUnit.id) return Ok();

            await unitsService.Data.Update(id, "parent", parentUnit.id);
            if (unit.branch != parentUnit.branch) {
                await unitsService.Data.Update(id, "branch", parentUnit.branch);
            }

            List<Unit> parentChildren = unitsService.Data.Get(x => x.parent == parentUnit.id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.id == unit.id));
            parentChildren.Insert(unitUpdate.index, unit);
            foreach (Unit child in parentChildren) {
                await unitsService.Data.Update(child.id, "order", parentChildren.IndexOf(child));
            }

            unit = unitsService.Data.GetSingle(unit.id);
            foreach (Unit child in unitsService.GetAllChildren(unit, true)) {
                foreach (string accountId in child.members) {
                    await assignmentService.UpdateGroupsAndRoles(accountId);
                }
            }

            return Ok();
        }

        [HttpPatch("{id}/order"), Authorize]
        public IActionResult UpdateSortOrder([FromRoute] string id, [FromBody] RequestUnitUpdateOrder unitUpdate) {
            Unit unit = unitsService.Data.GetSingle(id);
            Unit parentUnit = unitsService.Data.GetSingle(x => x.id == unit.parent);
            List<Unit> parentChildren = unitsService.Data.Get(x => x.parent == parentUnit.id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.id == unit.id));
            parentChildren.Insert(unitUpdate.index, unit);
            foreach (Unit child in parentChildren) {
                unitsService.Data.Update(child.id, "order", parentChildren.IndexOf(child));
            }

            return Ok();
        }

        // TODO: Use mappers/factories
        [HttpGet("chart/{type}"), Authorize]
        public IActionResult GetUnitsChart([FromRoute] string type) {
            switch (type) {
                case "combat":
                    Unit combatRoot = unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
                    return Ok(
                        new ResponseUnitChartNode {
                            id = combatRoot.id,
                            name = combatRoot.preferShortname ? combatRoot.shortname : combatRoot.name,
                            members = MapUnitMembers(combatRoot),
                            children = GetUnitChartChildren(combatRoot.id)
                        }
                    );
                case "auxiliary":
                    Unit auxiliaryRoot = unitsService.Data.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
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
            return unitsService.Data.Get(x => x.parent == parent)
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
            new ResponseUnitMember { name = displayNameService.GetDisplayName(member), role = member.roleAssignment, unitRole = GetRole(unit, member.id) };

        // TODO: Move somewhere else
        private IEnumerable<Account> SortMembers(IEnumerable<string> members, Unit unit) {
            return members.Select(
                              x => {
                                  Account account = accountService.Data.GetSingle(x);
                                  return new { account, rankIndex = ranksService.GetRankOrder(account.rank), roleIndex = unitsService.GetMemberRoleOrder(account, unit) };
                              }
                          )
                          .OrderByDescending(x => x.roleIndex)
                          .ThenBy(x => x.rankIndex)
                          .ThenBy(x => x.account.lastname)
                          .ThenBy(x => x.account.firstname)
                          .Select(x => x.account);
        }

        private string GetRole(Unit unit, string accountId) =>
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(0).name) ? "1" :
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(1).name) ? "2" :
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(2).name) ? "3" :
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(3).name) ? "N" : "";
    }
}
