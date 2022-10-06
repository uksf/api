using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class RanksController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAssignmentService _assignmentService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IRanksContext _ranksContext;

    public RanksController(
        IAccountContext accountContext,
        IRanksContext ranksContext,
        IAssignmentService assignmentService,
        INotificationsService notificationsService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _ranksContext = ranksContext;
        _assignmentService = assignmentService;
        _notificationsService = notificationsService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public IEnumerable<DomainRank> GetRanks()
    {
        return _ranksContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public IEnumerable<DomainRank> GetRanks([FromRoute] string id)
    {
        var domainAccount = _accountContext.GetSingle(id);
        return _ranksContext.Get(x => x.Name != domainAccount.Rank);
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
            return _ranksContext.GetSingle(x => x.Id != safeRank.Id && (x.Name == check || x.TeamspeakGroup == check));
        }

        return _ranksContext.GetSingle(x => x.Name == check || x.TeamspeakGroup == check);
    }

    [HttpPost]
    [Authorize]
    public DomainRank CheckRank([FromBody] DomainRank rank)
    {
        return rank == null ? null : _ranksContext.GetSingle(x => x.Id != rank.Id && (x.Name == rank.Name || x.TeamspeakGroup == rank.TeamspeakGroup));
    }

    [HttpPut]
    [Authorize]
    public async Task AddRank([FromBody] DomainRank rank)
    {
        await _ranksContext.Add(rank);
        _logger.LogAudit($"Rank added '{rank.Name}, {rank.Abbreviation}, {rank.TeamspeakGroup}'");
    }

    [HttpPatch]
    [Authorize]
    public async Task<IEnumerable<DomainRank>> EditRank([FromBody] DomainRank rank)
    {
        var oldRank = _ranksContext.GetSingle(x => x.Id == rank.Id);
        _logger.LogAudit(
            $"Rank updated from '{oldRank.Name}, {oldRank.Abbreviation}, {oldRank.TeamspeakGroup}, {oldRank.DiscordRoleId}' to '{rank.Name}, {rank.Abbreviation}, {rank.TeamspeakGroup}, {rank.DiscordRoleId}'"
        );
        await _ranksContext.Update(
            rank.Id,
            Builders<DomainRank>.Update.Set(x => x.Name, rank.Name)
                                .Set(x => x.Abbreviation, rank.Abbreviation)
                                .Set(x => x.TeamspeakGroup, rank.TeamspeakGroup)
                                .Set(x => x.DiscordRoleId, rank.DiscordRoleId)
        );
        foreach (var account in _accountContext.Get(x => x.Rank == oldRank.Name))
        {
            var notification = await _assignmentService.UpdateUnitRankAndRole(account.Id, rankString: rank.Name, reason: $"the '{rank.Name}' rank was updated");
            _notificationsService.Add(notification);
        }

        return _ranksContext.Get();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IEnumerable<DomainRank>> DeleteRank([FromRoute] string id)
    {
        var rank = _ranksContext.GetSingle(x => x.Id == id);
        _logger.LogAudit($"Rank deleted '{rank.Name}'");
        await _ranksContext.Delete(id);
        foreach (var account in _accountContext.Get(x => x.Rank == rank.Name))
        {
            var notification = await _assignmentService.UpdateUnitRankAndRole(
                account.Id,
                rankString: AssignmentService.RemoveFlag,
                reason: $"the '{rank.Name}' rank was deleted"
            );
            _notificationsService.Add(notification);
        }

        return _ranksContext.Get();
    }

    [HttpPost("order")]
    [Authorize]
    public async Task<IEnumerable<DomainRank>> UpdateOrder([FromBody] List<DomainRank> newRankOrder)
    {
        for (var index = 0; index < newRankOrder.Count; index++)
        {
            var rank = newRankOrder[index];
            if (_ranksContext.GetSingle(rank.Name).Order != index)
            {
                await _ranksContext.Update(rank.Id, x => x.Order, index);
            }
        }

        return _ranksContext.Get();
    }
}
