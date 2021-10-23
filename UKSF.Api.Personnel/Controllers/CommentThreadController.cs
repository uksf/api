using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("commentthread"), Permissions(Permissions.CONFIRMED, Permissions.MEMBER, Permissions.DISCHARGED)]
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

        [HttpGet("{id}"), Authorize]
        public CommentThreadsDataset Get(string id)
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

        [HttpGet("canpost/{id}"), Authorize]
        public bool GetCanPostComment(string id)
        {
            var commentThread = _commentThreadContext.GetSingle(id);
            var domainAccount = _accountService.GetUserAccount();
            var admin = _httpContextService.UserHasPermission(Permissions.ADMIN);
            var canPost = commentThread.Mode switch
            {
                ThreadMode.RECRUITER           => commentThread.Authors.Any(x => x == _httpContextService.GetUserId()) || admin || _recruitmentService.IsRecruiter(_accountService.GetUserAccount()),
                ThreadMode.RANKSUPERIOR        => commentThread.Authors.Any(x => admin || _ranksService.IsSuperior(domainAccount.Rank, _accountContext.GetSingle(x).Rank)),
                ThreadMode.RANKEQUAL           => commentThread.Authors.Any(x => admin || _ranksService.IsEqual(domainAccount.Rank, _accountContext.GetSingle(x).Rank)),
                ThreadMode.RANKSUPERIOROREQUAL => commentThread.Authors.Any(x => admin || _ranksService.IsSuperiorOrEqual(domainAccount.Rank, _accountContext.GetSingle(x).Rank)),
                _                              => true
            };

            return canPost;
        }

        [HttpPut("{commentThreadId}"), Authorize]
        public async Task AddComment(string commentThreadId, [FromBody] Comment comment)
        {
            comment.Id = ObjectId.GenerateNewId().ToString();
            comment.Timestamp = DateTime.Now;
            comment.Author = _httpContextService.GetUserId();
            await _commentThreadService.InsertComment(commentThreadId, comment);

            var thread = _commentThreadContext.GetSingle(commentThreadId);
            var participants = _commentThreadService.GetCommentThreadParticipants(thread.Id);
            var applicationAccount = _accountContext.GetSingle(
                x => x.Application?.ApplicationCommentThread == commentThreadId || x.Application?.RecruiterCommentThread == commentThreadId
            );

            foreach (var participant in participants.Where(x => x != comment.Author))
            {
                var url = _environment.IsDevelopment() ? "http://localhost:4200" : "https://uk-sf.co.uk";
                var link = applicationAccount.Id != participant ? $"{url}/recruitment/{applicationAccount.Id}" : $"{url}/application";
                _notificationsService.Add(
                    new()
                    {
                        Owner = participant,
                        Icon = NotificationIcons.COMMENT,
                        Message = $"{_displayNameService.GetDisplayName(comment.Author)} replied to a comment:\n\"{comment.Content}\"",
                        Link = link
                    }
                );
            }
        }

        [HttpPost("{id}/{commentId}"), Authorize]
        public async Task DeleteComment(string id, string commentId)
        {
            var comments = _commentThreadService.GetCommentThreadComments(id).ToList();
            var comment = comments.FirstOrDefault(x => x.Id == commentId);
            await _commentThreadService.RemoveComment(id, comment);
        }
    }
}
