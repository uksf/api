using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class UnitsController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly IDiscordService discordService;
        private readonly IDisplayNameService displayNameService;
        private readonly IRanksService ranksService;
        private readonly IRolesService rolesService;
        private readonly IServerService serverService;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;
        private readonly INotificationsService notificationsService;

        public UnitsController(
            ISessionService sessionService,
            IAccountService accountService,
            IDisplayNameService displayNameService,
            IRanksService ranksService,
            IUnitsService unitsService,
            IRolesService rolesService,
            ITeamspeakService teamspeakService,
            IAssignmentService assignmentService,
            IServerService serverService,
            IDiscordService discordService,
            INotificationsService notificationsService
        ) {
            this.sessionService = sessionService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.rolesService = rolesService;
            this.teamspeakService = teamspeakService;
            this.assignmentService = assignmentService;
            this.serverService = serverService;
            this.discordService = discordService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IActionResult Get() => Ok(unitsService.GetSortedUnits());

        [HttpGet("{id}"), Authorize]
        public IActionResult GetAccountUnits(string id, [FromQuery] string filter = "") {
            switch (filter) {
                case "auxiliary": return Ok(unitsService.GetSortedUnits(x => x.branch == UnitBranch.AUXILIARY && x.members.Contains(id)));
                case "available": return Ok(unitsService.GetSortedUnits(x => !x.members.Contains(id)));
                default: return Ok(unitsService.GetSortedUnits(x => x.members.Contains(id)));
            }
        }

        [HttpGet("tree"), Authorize]
        public IActionResult GetTree() {
            Unit combatRoot = unitsService.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
            Unit auxiliaryRoot = unitsService.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
            return Ok(new {combatUnits = new[] {new {combatRoot.id, combatRoot.name, children = GetTreeChildren(combatRoot), unit = combatRoot}}, auxiliaryUnits = new[] {new {auxiliaryRoot.id, auxiliaryRoot.name, children = GetTreeChildren(auxiliaryRoot), unit = auxiliaryRoot}}});
        }

        private List<object> GetTreeChildren(Unit parent) {
            List<object> children = new List<object>();
            foreach (Unit unit in unitsService.Get(x => x.parent == parent.id).OrderBy(x => x.order)) {
                children.Add(new {unit.id, unit.name, children = GetTreeChildren(unit), unit});
            }

            return children;
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckUnit(string check, [FromBody] Unit unit = null) {
            if (string.IsNullOrEmpty(check)) return Ok();
            return Ok(
                unit != null
                    ? unitsService.GetSingle(x => x.id != unit.id && (x.name == check || x.shortname == check || x.teamspeakGroup == check || x.discordRoleId == check || x.callsign == check))
                    : unitsService.GetSingle(x => x.name == check || x.shortname == check || x.teamspeakGroup == check || x.discordRoleId == check || x.callsign == check)
            );
        }

        [HttpPost, Authorize]
        public IActionResult CheckUnit([FromBody] Unit unit) {
            return unit != null ? (IActionResult) Ok(unitsService.GetSingle(x => x.id != unit.id && (x.name == unit.name || x.shortname == unit.shortname || x.teamspeakGroup == unit.teamspeakGroup || x.discordRoleId == unit.discordRoleId || x.callsign == unit.callsign))) : Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddUnit([FromBody] Unit unit) {
            await unitsService.Add(unit);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"New unit added '{unit.name}, {unit.shortname}, {unit.type}, {unit.branch}, {unit.teamspeakGroup}, {unit.discordRoleId}, {unit.callsign}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditUnit([FromBody] Unit unit) {
            Unit localUnit = unit;
            Unit oldUnit = unitsService.GetSingle(x => x.id == localUnit.id);
            LogWrapper.AuditLog(
                sessionService.GetContextId(),
                $"Unit updated from '{oldUnit.name}, {oldUnit.shortname}, {oldUnit.type}, {oldUnit.parent}, {oldUnit.branch}, {oldUnit.teamspeakGroup}, {oldUnit.discordRoleId}, {oldUnit.callsign}, {oldUnit.icon}' to '{unit.name}, {unit.shortname}, {unit.type}, {unit.parent}, {unit.branch}, {unit.teamspeakGroup}, {unit.discordRoleId}, {unit.callsign}, {unit.icon}'"
            );
            await unitsService.Update(
                unit.id,
                Builders<Unit>.Update.Set("name", unit.name)
                              .Set("shortname", unit.shortname)
                              .Set("type", unit.type)
                              .Set("parent", unit.parent)
                              .Set("branch", unit.branch)
                              .Set("teamspeakGroup", unit.teamspeakGroup)
                              .Set("discordRoleId", unit.discordRoleId)
                              .Set("callsign", unit.callsign)
                              .Set("icon", unit.icon)
            );
            unit = unitsService.GetSingle(unit.id);
            if (unit.name != oldUnit.name) {
                foreach (Account account in accountService.Get(x => x.unitAssignment == oldUnit.name)) {
                    await accountService.Update(account.id, "unitAssignment", unit.name);
                    teamspeakService.UpdateAccountTeamspeakGroups(accountService.GetSingle(account.id));
                }
            }

            if (unit.teamspeakGroup != oldUnit.teamspeakGroup) {
                foreach (Account account in unit.members.Select(x => accountService.GetSingle(x))) {
                    teamspeakService.UpdateAccountTeamspeakGroups(account);
                }
            }

            if (unit.discordRoleId != oldUnit.discordRoleId) {
                foreach (Account account in unit.members.Select(x => accountService.GetSingle(x))) {
                    await discordService.UpdateAccount(account);
                }
            }

            serverService.UpdateSquadXml();
            return Ok();
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteUnit(string id) {
            Unit unit = unitsService.GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Unit deleted '{unit.name}'");
            foreach (Account account in accountService.Get(x => x.unitAssignment == unit.name)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, "Reserves", reason: $"{unit.name} was deleted");
                notificationsService.Add(notification);
            }

            await unitsService.Delete(id);
            serverService.UpdateSquadXml();
            return Ok();
        }

        [HttpPost("parent"), Authorize]
        public async Task<IActionResult> UpdateParent([FromBody] JObject data) {
            Unit unit = JsonConvert.DeserializeObject<Unit>(data["unit"].ToString());
            Unit parentUnit = JsonConvert.DeserializeObject<Unit>(data["parentUnit"].ToString());
            if (unit.parent == parentUnit.id) return Ok();
            await unitsService.Update(unit.id, "parent", parentUnit.id);

            if (unit.branch != parentUnit.branch) {
                await unitsService.Update(unit.id, "branch", parentUnit.branch);
            }

            List<Unit> parentChildren = unitsService.Get(x => x.parent == parentUnit.id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.id == unit.id));
            parentChildren.Add(unit);
            foreach (Unit child in parentChildren) {
                await unitsService.Update(child.id, "order", parentChildren.IndexOf(child));
            }

            unit = unitsService.GetSingle(unit.id);
            foreach (Unit child in unitsService.GetAllChildren(unit, true)) {
                foreach (Account account in child.members.Select(x => accountService.GetSingle(x))) {
                    Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, unit.name, reason: $"the hierarchy chain for {unit.name} was updated");
                    notificationsService.Add(notification);
                }
            }

            return Ok();
        }

        [HttpPost("order"), Authorize]
        public IActionResult UpdateSortOrder([FromBody] JObject data) {
            Unit unit = JsonConvert.DeserializeObject<Unit>(data["unit"].ToString());
            int index = JsonConvert.DeserializeObject<int>(data["index"].ToString());
            Unit parentUnit = unitsService.GetSingle(x => x.id == unit.parent);
            List<Unit> parentChildren = unitsService.Get(x => x.parent == parentUnit.id).ToList();
            parentChildren.Remove(parentChildren.FirstOrDefault(x => x.id == unit.id));
            parentChildren.Insert(index, unit);
            foreach (Unit child in parentChildren) {
                unitsService.Update(child.id, "order", parentChildren.IndexOf(child));
            }

            return Ok();
        }

        [HttpGet("filter"), Authorize]
        public IActionResult Get([FromQuery] string typeFilter) {
            switch (typeFilter) {
                case "regiments":
                    string combatRootId = unitsService.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT).id;
                    return Ok(unitsService.Get(x => x.parent == combatRootId || x.id == combatRootId).ToList().Select(x => new {x.name, x.shortname, id = x.id.ToString(), x.icon}));
                case "orgchart":
                    Unit combatRoot = unitsService.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.COMBAT);
                    return Ok(
                        new[] {
                            new {
                                combatRoot.id,
                                label = combatRoot.shortname,
                                type = "person",
                                styleClass = "ui-person",
                                expanded = true,
                                data = new {name = combatRoot.members != null ? SortMembers(combatRoot.members, combatRoot).Select(y => new {role = GetRole(combatRoot, y), name = displayNameService.GetDisplayName(y)}) : null},
                                children = GetChartChildren(combatRoot.id)
                            }
                        }
                    );
                case "orgchartaux":
                    Unit auziliaryRoot = unitsService.GetSingle(x => x.parent == ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY);
                    return Ok(
                        new[] {
                            new {
                                auziliaryRoot.id,
                                label = auziliaryRoot.name,
                                type = "person",
                                styleClass = "ui-person",
                                expanded = true,
                                data = new {name = auziliaryRoot.members != null ? SortMembers(auziliaryRoot.members, auziliaryRoot).Select(y => new {role = GetRole(auziliaryRoot, y), name = displayNameService.GetDisplayName(y)}) : null},
                                children = GetChartChildren(auziliaryRoot.id)
                            }
                        }
                    );
                default: return Ok(unitsService.Get().Select(x => new {viewValue = x.name, value = x.id.ToString()}));
            }
        }

        [HttpGet("info/{id}"), Authorize]
        public IActionResult GetInfo(string id) {
            Unit unit = unitsService.GetSingle(id);
            IEnumerable<Unit> parents = unitsService.GetParents(unit).ToList();
            Unit regiment = parents.Skip(1).FirstOrDefault(x => x.type == UnitType.REGIMENT);
            return Ok(
                new {
                    unitData = unit,
                    unit.parent,
                    type = unit.type.ToString(),
                    displayName = unit.name,
//                    oic = unit.roles == null || !unitsService.UnitHasRole(unit, rolesService.GetUnitRoleByOrder(0).name) ? "" : displayNameService.GetDisplayName(accountService.GetSingle(unit.roles[rolesService.GetUnitRoleByOrder(0).name])),
//                    xic = unit.roles == null || !unitsService.UnitHasRole(unit, rolesService.GetUnitRoleByOrder(1).name) ? "" : displayNameService.GetDisplayName(accountService.GetSingle(unit.roles[rolesService.GetUnitRoleByOrder(1).name])),
//                    ncoic = unit.roles == null || !unitsService.UnitHasRole(unit, rolesService.GetUnitRoleByOrder(2).name) ? "" : displayNameService.GetDisplayName(accountService.GetSingle(unit.roles[rolesService.GetUnitRoleByOrder(2).name])),
                    code = unitsService.GetChainString(unit),
                    parentDisplay = parents.Skip(1).FirstOrDefault()?.name,
                    regimentDisplay = regiment?.name,
                    parentURL = parents.Skip(1).FirstOrDefault()?.id,
                    regimentURL = regiment?.id,
//                    attendance = "10 instances (70% rate)",
//                    absences = "10 instances (70% rate)",
//                    coverageLOA = "80%",
//                    casualtyRate = "10010 instances (70% rate)",
//                    fatalityRate = "200 instances (20% rate)",
                    memberCollection = unit.members.Select(x => accountService.GetSingle(x)).Select(x => new {name = displayNameService.GetDisplayName(x), role = x.roleAssignment})
                }
            );
        }

        [HttpGet("members/{id}"), Authorize]
        public IActionResult GetMembers(string id) {
            Unit unit = unitsService.GetSingle(id);
            return Ok(unit.members.Select(x => accountService.GetSingle(x)).Select(x => new {name = displayNameService.GetDisplayName(x), role = x.roleAssignment}));
        }

        private object[] GetChartChildren(string parent) {
            List<object> units = new List<object>();
            foreach (Unit unit in unitsService.Get(x => x.parent == parent)) {
                units.Add(
                    new {
                        unit.id,
                        label = unit.type == UnitType.PLATOON || unit.type == UnitType.SECTION || unit.type == UnitType.SRTEAM ? unit.name : unit.shortname,
                        type = "person",
                        styleClass = "ui-person",
                        expanded = true,
                        data = new {name = unit.members != null ? SortMembers(unit.members, unit).Select(y => new {role = GetRole(unit, y), name = displayNameService.GetDisplayName(y)}) : null},
                        children = GetChartChildren(unit.id)
                    }
                );
            }

            return units.ToArray();
        }

        private string GetRole(Unit unit, string accountId) =>
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(0).name) ? "1" :
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(1).name) ? "2" :
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(2).name) ? "3" :
            unitsService.MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(3).name) ? "N" : "";

        private IEnumerable<string> SortMembers(IEnumerable<string> members, Unit unit) {
            var accounts = members.Select(
                                      x => {
                                          Account account = accountService.GetSingle(x);
                                          return new {account, rankIndex = ranksService.GetRankIndex(account.rank), roleIndex = unitsService.GetMemberRoleOrder(account, unit)};
                                      }
                                  )
                                  .ToList();
            accounts.Sort(
                (a, b) => a.roleIndex < b.roleIndex ? 1 :
                    a.roleIndex > b.roleIndex ? -1 :
                    a.rankIndex < b.rankIndex ? -1 :
                    a.rankIndex > b.rankIndex ? 1 : string.CompareOrdinal(a.account.lastname, b.account.lastname)
            );
            return accounts.Select(x => x.account.id);
        }
    }
}
