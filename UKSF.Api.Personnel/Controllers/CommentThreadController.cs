using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("commentthread"), Permissions(Permissions.CONFIRMED, Permissions.MEMBER, Permissions.DISCHARGED)]
    public class CommentThreadController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IAccountService _accountService;
        private readonly ICommentThreadContext _commentThreadContext;
        private readonly ICommentThreadService _commentThreadService;
        private readonly IDisplayNameService _displayNameService;
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
            IHttpContextService httpContextService
        ) {
            _accountContext = accountContext;
            _commentThreadContext = commentThreadContext;
            _commentThreadService = commentThreadService;
            _ranksService = ranksService;
            _accountService = accountService;
            _displayNameService = displayNameService;
            _recruitmentService = recruitmentService;
            _notificationsService = notificationsService;
            _httpContextService = httpContextService;
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) {
            IEnumerable<Comment> comments = _commentThreadService.GetCommentThreadComments(id);
            return Ok(
                new {
                    comments = comments.Select(
                        comment => new {
                            Id = comment.Id.ToString(),
                            Author = comment.Author.ToString(),
                            DisplayName = _displayNameService.GetDisplayName(_accountContext.GetSingle(comment.Author)),
                            comment.Content,
                            comment.Timestamp
                        }
                    )
                }
            );
        }

        [HttpGet("canpost/{id}"), Authorize]
        public IActionResult GetCanPostComment(string id) {
            CommentThread commentThread = _commentThreadContext.GetSingle(id);
            Account account = _accountService.GetUserAccount();
            bool admin = _httpContextService.UserHasPermission(Permissions.ADMIN);
            bool canPost = commentThread.Mode switch {
                ThreadMode.RECRUITER           => commentThread.Authors.Any(x => x == _httpContextService.GetUserId()) || admin || _recruitmentService.IsRecruiter(_accountService.GetUserAccount()),
                ThreadMode.RANKSUPERIOR        => commentThread.Authors.Any(x => admin || _ranksService.IsSuperior(account.Rank, _accountContext.GetSingle(x).Rank)),
                ThreadMode.RANKEQUAL           => commentThread.Authors.Any(x => admin || _ranksService.IsEqual(account.Rank, _accountContext.GetSingle(x).Rank)),
                ThreadMode.RANKSUPERIOROREQUAL => commentThread.Authors.Any(x => admin || _ranksService.IsSuperiorOrEqual(account.Rank, _accountContext.GetSingle(x).Rank)),
                _                              => true
            };

            return Ok(new { canPost });
        }

        [HttpPut("{commentThreadId}"), Authorize]
        public async Task<IActionResult> AddComment(string commentThreadId, [FromBody] Comment comment) {
            comment.Id = ObjectId.GenerateNewId().ToString();
            comment.Timestamp = DateTime.Now;
            comment.Author = _httpContextService.GetUserId();
            await _commentThreadService.InsertComment(commentThreadId, comment);
            CommentThread thread = _commentThreadContext.GetSingle(commentThreadId);
            IEnumerable<string> participants = _commentThreadService.GetCommentThreadParticipants(thread.Id);
            foreach (string objectId in participants.Where(x => x != comment.Author)) {
                _notificationsService.Add( // TODO: Set correct link when comment thread is between /application and /recruitment/id
                    new Notification {
                        Owner = objectId,
                        Icon = NotificationIcons.COMMENT,
                        Message = $"{_displayNameService.GetDisplayName(comment.Author)} replied to a comment:\n\"{comment.Content}\"",
                        Link = HttpContext.Request.Headers["Referer"].ToString().Replace("http://localhost:4200", "").Replace("https://www.uk-sf.co.uk", "").Replace("https://uk-sf.co.uk", "")
                    }
                );
            }

            return Ok();
        }

        [HttpPost("{id}/{commentId}"), Authorize]
        public async Task<IActionResult> DeleteComment(string id, string commentId) {
            List<Comment> comments = _commentThreadService.GetCommentThreadComments(id).ToList();
            Comment comment = comments.FirstOrDefault(x => x.Id == commentId);
            await _commentThreadService.RemoveComment(id, comment);
            return Ok();
        }
    }
}
