using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers {
    [Route("[controller]")]
    public class RanksController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly INotificationsService notificationsService;
        private readonly IRanksService ranksService;
        private readonly ISessionService sessionService;

        public RanksController(IRanksService ranksService, IAccountService accountService, IAssignmentService assignmentService, ISessionService sessionService, INotificationsService notificationsService) {
            this.ranksService = ranksService;
            this.accountService = accountService;
            this.assignmentService = assignmentService;
            this.sessionService = sessionService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IActionResult GetRanks() => Ok(ranksService.Data().Get());

        [HttpGet("{id}"), Authorize]
        public IActionResult GetRanks(string id) {
            Account account = accountService.Data().GetSingle(id);
            return Ok(ranksService.Data().Get(x => x.name != account.rank));
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckRank(string check, [FromBody] Rank rank = null) {
            if (string.IsNullOrEmpty(check)) return Ok();
            if (rank != null) {
                Rank safeRank = rank;
                return Ok(ranksService.Data().GetSingle(x => x.id != safeRank.id && (x.name == check || x.teamspeakGroup == check)));
            }

            return Ok(ranksService.Data().GetSingle(x => x.name == check || x.teamspeakGroup == check));
        }

        [HttpPost, Authorize]
        public IActionResult CheckRank([FromBody] Rank rank) {
            return rank != null ? (IActionResult) Ok(ranksService.Data().GetSingle(x => x.id != rank.id && (x.name == rank.name || x.teamspeakGroup == rank.teamspeakGroup))) : Ok();
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddRank([FromBody] Rank rank) {
            await ranksService.Data().Add(rank);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Rank added '{rank.name}, {rank.abbreviation}, {rank.teamspeakGroup}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditRank([FromBody] Rank rank) {
            Rank oldRank = ranksService.Data().GetSingle(x => x.id == rank.id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Rank updated from '{oldRank.name}, {oldRank.abbreviation}, {oldRank.teamspeakGroup}, {oldRank.discordRoleId}' to '{rank.name}, {rank.abbreviation}, {rank.teamspeakGroup}, {rank.discordRoleId}'");
            await ranksService.Data().Update(rank.id, Builders<Rank>.Update.Set("name", rank.name).Set("abbreviation", rank.abbreviation).Set("teamspeakGroup", rank.teamspeakGroup).Set("discordRoleId", rank.discordRoleId));
            foreach (Account account in accountService.Data().Get(x => x.rank == oldRank.name)) {
                await accountService.Data().Update(account.id, "rank", rank.name);
            }

            return Ok(ranksService.Data().Get());
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteRank(string id) {
            Rank rank = ranksService.Data().GetSingle(x => x.id == id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Rank deleted '{rank.name}'");
            await ranksService.Data().Delete(id);
            foreach (Account account in accountService.Data().Get(x => x.rank == rank.name)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, rankString: AssignmentService.REMOVE_FLAG, reason: $"the '{rank.name}' rank was deleted");
                notificationsService.Add(notification);
            }

            return Ok(ranksService.Data().Get());
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<Rank> newRankOrder) {
            for (int index = 0; index < newRankOrder.Count; index++) {
                Rank rank = newRankOrder[index];
                if (ranksService.Data().GetSingle(rank.name).order != index) {
                    await ranksService.Data().Update(rank.id, "order", index);
                }
            }

            return Ok(ranksService.Data().Get());
        }
    }
}
