using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class RanksController(
    IAccountContext accountContext,
    IRanksContext ranksContext,
    IAssignmentService assignmentService,
    INotificationsService notificationsService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainRank> GetRanks()
    {
        return ranksContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public IEnumerable<DomainRank> GetRanks([FromRoute] string id)
    {
        var account = accountContext.GetSingle(id);
        return ranksContext.Get(x => x.Name != account.Rank);
    }

    [HttpPost("{check}")]
    [Authorize]
    public DomainRank CheckRank([FromRoute] string check, [FromBody] DomainRank rank = null)
    {
        if (string.IsNullOrEmpty(check))
        {
            return null;
        }

        if (rank != null)
        {
            var safeRank = rank;
            return ranksContext.GetSingle(x => x.Id != safeRank.Id && (x.Name == check || x.TeamspeakGroup == check));
        }

        return ranksContext.GetSingle(x => x.Name == check || x.TeamspeakGroup == check);
    }

    [HttpPost("exists")]
    [Authorize]
    public DomainRank CheckRank([FromBody] DomainRank rank)
    {
        return rank == null ? null : ranksContext.GetSingle(x => x.Id != rank.Id && (x.Name == rank.Name || x.TeamspeakGroup == rank.TeamspeakGroup));
    }

    [HttpPost]
    [Authorize]
    public async Task AddRank([FromBody] DomainRank rank)
    {
        await ranksContext.Add(rank);
        logger.LogAudit($"Rank added '{rank.Name}, {rank.Abbreviation}, {rank.TeamspeakGroup}'");
    }

    [HttpPatch]
    [Authorize]
    public async Task<IEnumerable<DomainRank>> EditRank([FromBody] DomainRank rank)
    {
        var oldRank = ranksContext.GetSingle(x => x.Id == rank.Id);
        logger.LogAudit(
            $"Rank updated from '{oldRank.Name}, {oldRank.Abbreviation}, {oldRank.TeamspeakGroup}, {oldRank.DiscordRoleId}' to '{rank.Name}, {rank.Abbreviation}, {rank.TeamspeakGroup}, {rank.DiscordRoleId}'"
        );
        await ranksContext.Update(
            rank.Id,
            Builders<DomainRank>.Update.Set(x => x.Name, rank.Name)
                                .Set(x => x.Abbreviation, rank.Abbreviation)
                                .Set(x => x.TeamspeakGroup, rank.TeamspeakGroup)
                                .Set(x => x.DiscordRoleId, rank.DiscordRoleId)
        );
        foreach (var account in accountContext.Get(x => x.Rank == oldRank.Name))
        {
            var notification = await assignmentService.UpdateUnitRankAndRole(account.Id, rankString: rank.Name, reason: $"the '{rank.Name}' rank was updated");
            notificationsService.Add(notification);
        }

        return ranksContext.Get();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IEnumerable<DomainRank>> DeleteRank([FromRoute] string id)
    {
        var rank = ranksContext.GetSingle(x => x.Id == id);
        logger.LogAudit($"Rank deleted '{rank.Name}'");
        await ranksContext.Delete(id);
        foreach (var account in accountContext.Get(x => x.Rank == rank.Name))
        {
            var notification = await assignmentService.UpdateUnitRankAndRole(
                account.Id,
                rankString: AssignmentService.RemoveFlag,
                reason: $"the '{rank.Name}' rank was deleted"
            );
            notificationsService.Add(notification);
        }

        return ranksContext.Get();
    }

    [HttpPost("order")]
    [Authorize]
    public async Task<IEnumerable<DomainRank>> UpdateOrder([FromBody] List<DomainRank> newRankOrder)
    {
        for (var index = 0; index < newRankOrder.Count; index++)
        {
            var rank = newRankOrder[index];
            if (ranksContext.GetSingle(rank.Name).Order != index)
            {
                await ranksContext.Update(rank.Id, x => x.Order, index);
            }
        }

        return ranksContext.Get();
    }
}
