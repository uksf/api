using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("commentthread")]
[Permissions(Permissions.Confirmed, Permissions.Member, Permissions.Discharged)]
public class CommentThreadController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountService _accountService;
    private readonly ICommentThreadContext _commentThreadContext;
    private readonly ICommentThreadService _commentThreadService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IHostEnvironment _environment;
    private readonly IHttpContextService _httpContextService;
    private readonly INotificationsService _notificationsService;
    private readonly IRanksService _ranksService;
    private readonly IRecruitmentService _recruitmentService;

    public CommentThreadController(
        IAccountContext accountContext,
        ICommentThreadContext commentThreadContext,
        ICommentThreadService commentThreadService,
        IRanksService ranksService,
        IAccountService accountService,
        IDisplayNameService displayNameService,
        IRecruitmentService recruitmentService,
        INotificationsService notificationsService,
        IHttpContextService httpContextService,
        IHostEnvironment environment
    )
    {
        _accountContext = accountContext;
        _commentThreadContext = commentThreadContext;
        _commentThreadService = commentThreadService;
        _ranksService = ranksService;
        _accountService = accountService;
        _displayNameService = displayNameService;
        _recruitmentService = recruitmentService;
        _notificationsService = notificationsService;
        _httpContextService = httpContextService;
        _environment = environment;
    }

    [HttpGet("{id}")]
    [Authorize]
    public CommentThreadsDataset Get([FromRoute] string id)
    {
        var comments = _commentThreadService.GetCommentThreadComments(id);
        return new()
        {
            Comments = comments.Select(
                comment => new CommentThreadDataset
                {
                    Id = comment.Id.ToString(),
                    Author = comment.Author.ToString(),
                    DisplayName = _displayNameService.GetDisplayName(comment.Author),
                    Content = comment.Content,
                    Timestamp = comment.Timestamp
                }
            )
        };
    }

    [HttpGet("canpost/{id}")]
    [Authorize]
    public bool GetCanPostComment([FromRoute] string id)
    {
        var commentThread = _commentThreadContext.GetSingle(id);
        var domainAccount = _accountService.GetUserAccount();
        var admin = _httpContextService.UserHasPermission(Permissions.Admin);
        var canPost = commentThread.Mode switch
        {
            ThreadMode.RECRUITER => commentThread.Authors.Any(x => x == _httpContextService.GetUserId()) ||
                                    admin ||
                                    _recruitmentService.IsRecruiter(_accountService.GetUserAccount()),
            ThreadMode.RANKSUPERIOR => commentThread.Authors.Any(x => admin || _ranksService.IsSuperior(domainAccount.Rank, _accountContext.GetSingle(x).Rank)),
            ThreadMode.RANKEQUAL    => commentThread.Authors.Any(x => admin || _ranksService.IsEqual(domainAccount.Rank, _accountContext.GetSingle(x).Rank)),
            ThreadMode.RANKSUPERIOROREQUAL => commentThread.Authors.Any(
                x => admin || _ranksService.IsSuperiorOrEqual(domainAccount.Rank, _accountContext.GetSingle(x).Rank)
            ),
            _ => true
        };

        return canPost;
    }

    [HttpPut("{commentThreadId}")]
    [Authorize]
    public async Task AddComment([FromRoute] string commentThreadId, [FromBody] Comment comment)
    {
        comment.Id = ObjectId.GenerateNewId().ToString();
        comment.Timestamp = DateTime.UtcNow;
        comment.Author = _httpContextService.GetUserId();
        await _commentThreadService.InsertComment(commentThreadId, comment);

        var thread = _commentThreadContext.GetSingle(commentThreadId);
        var participants = _commentThreadService.GetCommentThreadParticipants(thread.Id);
        var applicationAccount = _accountContext.GetSingle(
            x => x.Application?.ApplicationCommentThread == commentThreadId || x.Application?.RecruiterCommentThread == commentThreadId
        );

        foreach (var participant in participants.Where(x => x != comment.Author))
        {
            var link = applicationAccount.Id != participant ? $"/recruitment/{applicationAccount.Id}" : "/application";
            _notificationsService.Add(
                new()
                {
                    Owner = participant,
                    Icon = NotificationIcons.Comment,
                    Message = $"{_displayNameService.GetDisplayName(comment.Author)} replied to a comment:\n\"{comment.Content}\"",
                    Link = link
                }
            );
        }
    }

    [HttpPost("{id}/{commentId}")]
    [Authorize]
    public async Task DeleteComment([FromRoute] string id, [FromRoute] string commentId)
    {
        var comments = _commentThreadService.GetCommentThreadComments(id).ToList();
        var comment = comments.FirstOrDefault(x => x.Id == commentId);
        await _commentThreadService.RemoveComment(id, comment);
    }
}
