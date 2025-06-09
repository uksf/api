using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("commentthread")]
[Permissions(Permissions.Confirmed, Permissions.Member, Permissions.Discharged)]
public class CommentThreadController(
    IAccountContext accountContext,
    ICommentThreadContext commentThreadContext,
    ICommentThreadService commentThreadService,
    IRanksService ranksService,
    IAccountService accountService,
    IDisplayNameService displayNameService,
    IRecruitmentService recruitmentService,
    INotificationsService notificationsService,
    IHttpContextService httpContextService
) : ControllerBase
{
    [HttpGet("{id}")]
    [Authorize]
    public CommentThreadsDataset Get([FromRoute] string id)
    {
        var comments = commentThreadService.GetCommentThreadComments(id);
        return new CommentThreadsDataset
        {
            Comments = comments.Select(comment => new CommentThreadDataset
                {
                    Id = comment.Id.ToString(),
                    Author = comment.Author.ToString(),
                    DisplayName = displayNameService.GetDisplayName(comment.Author),
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
        var commentThread = commentThreadContext.GetSingle(id);
        var account = accountService.GetUserAccount();
        var admin = httpContextService.UserHasPermission(Permissions.Admin);
        var canPost = commentThread.Mode switch
        {
            ThreadMode.Recruiter => commentThread.Authors.Any(x => x == httpContextService.GetUserId()) ||
                                    admin ||
                                    recruitmentService.IsRecruiter(accountService.GetUserAccount()),
            ThreadMode.Ranksuperior => commentThread.Authors.Any(x => admin || ranksService.IsSuperior(account.Rank, accountContext.GetSingle(x).Rank)),
            ThreadMode.Rankequal    => commentThread.Authors.Any(x => admin || ranksService.IsEqual(account.Rank, accountContext.GetSingle(x).Rank)),
            ThreadMode.Ranksuperiororequal => commentThread.Authors.Any(x => admin ||
                                                                             ranksService.IsSuperiorOrEqual(account.Rank, accountContext.GetSingle(x).Rank)
            ),
            _ => true
        };

        return canPost;
    }

    [HttpPut("{commentThreadId}")]
    [Authorize]
    public async Task AddComment([FromRoute] string commentThreadId, [FromBody] DomainComment comment)
    {
        comment.Id = ObjectId.GenerateNewId().ToString();
        comment.Timestamp = DateTime.UtcNow;
        comment.Author = httpContextService.GetUserId();
        await commentThreadService.InsertComment(commentThreadId, comment);

        var thread = commentThreadContext.GetSingle(commentThreadId);
        var participants = commentThreadService.GetCommentThreadParticipants(thread.Id);
        var applicationAccount =
            accountContext.GetSingle(x => x.Application?.ApplicationCommentThread == commentThreadId || x.Application?.RecruiterCommentThread == commentThreadId
            );

        foreach (var participant in participants.Where(x => x != comment.Author))
        {
            var link = applicationAccount.Id != participant ? $"/recruitment/{applicationAccount.Id}" : "/application";
            notificationsService.Add(
                new DomainNotification
                {
                    Owner = participant,
                    Icon = NotificationIcons.Comment,
                    Message = $"{displayNameService.GetDisplayName(comment.Author)} replied to a comment:\n\"{comment.Content}\"",
                    Link = link
                }
            );
        }
    }

    [HttpPost("{id}/{commentId}")]
    [Authorize]
    public async Task DeleteComment([FromRoute] string id, [FromRoute] string commentId)
    {
        var comments = commentThreadService.GetCommentThreadComments(id).ToList();
        var comment = comments.FirstOrDefault(x => x.Id == commentId);
        await commentThreadService.RemoveComment(id, comment);
    }
}
