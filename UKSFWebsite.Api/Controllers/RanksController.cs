using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class RanksController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly IRanksService ranksService;
        private readonly ISessionService sessionService;
        private readonly INotificationsService notificationsService;

        public RanksController(IRanksService ranksService, IAccountService accountService, IAssignmentService assignmentService, ISessionService sessionService, INotificationsService notificationsService) {
            this.ranksService = ranksService;
            this.accountService = accountService;
            this.assignmentService = assignmentService;
            this.sessionService = sessionService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IActionResult GetRanks() => Ok(ranksService.Get());

        [HttpGet("{id}"), Authorize]
        public IActionResult GetRanks(string id) {
            Account account = accountService.GetSingle(id);
            return Ok(ranksService.Get(x => x.name != account.rank));
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckRank(string check, [FromBody] Rank rank = null) {
            if (string.IsNullOrEmpty(check)) return Ok();
            return Ok(rank != null ? ranksService.GetSingle(x => x.id != rank.id && (x.name == check || x.teamspeakGroup == check)) : ranksService.GetSingle(x => x.name == check || x.teamspeakGroup == check));
        }

        [HttpPost, Authorize]
        public IActionResult CheckRank([FromBody] Rank rank) {
            return rank != null ? (IActionResult) Ok(ranksService.GetSingle(x => x.id != rank.id && (x.name == rank.name || x.teamspeakGroup == rank.teamspeakGroup))) : Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddRank([FromBody] Rank rank) {
            await ranksService.Add(rank);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Rank added '{rank.name}, {rank.abbreviation}, {rank.teamspeakGroup}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditRank([FromBody] Rank rank) {
            Rank oldRank = ranksService.GetSingle(x => x.id == rank.id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Rank updated from '{oldRank.name}, {oldRank.abbreviation}, {oldRank.teamspeakGroup}, {oldRank.discordRoleId}' to '{rank.name}, {rank.abbreviation}, {rank.teamspeakGroup}, {rank.discordRoleId}'");
            await ranksService.Update(rank.id, Builders<Rank>.Update.Set("name", rank.name).Set("abbreviation", rank.abbreviation).Set("teamspeakGroup", rank.teamspeakGroup).Set("discordRoleId", rank.discordRoleId));
            foreach (Account account in accountService.Get(x => x.rank == oldRank.name)) {
                await accountService.Update(account.id, "rank", rank.name);
            }

            return Ok(ranksService.Get());
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteRank(string id) {
            Rank rank = ranksService.GetSingle(x => x.id == id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Rank deleted '{rank.name}'");
            await ranksService.Delete(id);
            foreach (Account account in accountService.Get(x => x.rank == rank.name)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, rankString: AssignmentService.REMOVE_FLAG, reason: $"the '{rank.name}' rank was deleted");
                notificationsService.Add(notification);
            }

            return Ok(ranksService.Get());
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<Rank> newRankOrder) {
            for (int index = 0; index < newRankOrder.Count; index++) {
                Rank rank = newRankOrder[index];
                if (ranksService.GetSingle(rank.name).order != index) {
                    await ranksService.Update(rank.id, "order", index);
                }
            }

            return Ok(ranksService.Get());
        }
    }
}
