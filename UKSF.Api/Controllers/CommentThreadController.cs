using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers {
    [Route("commentthread"), Roles(RoleDefinitions.CONFIRMED, RoleDefinitions.MEMBER, RoleDefinitions.DISCHARGED)]
    public class CommentThreadController : Controller {
        private readonly IAccountService accountService;
        private readonly ICommentThreadService commentThreadService;
        private readonly IDisplayNameService displayNameService;
        private readonly INotificationsService notificationsService;
        private readonly IRanksService ranksService;
        private readonly IRecruitmentService recruitmentService;
        private readonly ISessionService sessionService;

        public CommentThreadController(ICommentThreadService commentThreadService, ISessionService sessionService, IRanksService ranksService, IAccountService accountService, IDisplayNameService displayNameService, IRecruitmentService recruitmentService, INotificationsService notificationsService) {
            this.commentThreadService = commentThreadService;
            this.sessionService = sessionService;
            this.ranksService = ranksService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.recruitmentService = recruitmentService;
            this.notificationsService = notificationsService;
        }

        [HttpGet("{id}"), Authorize]
        public IActionResult Get(string id) {
            IEnumerable<Comment> comments = commentThreadService.GetCommentThreadComments(id);
            return Ok(
                new {
                    comments = comments.Select(
                        comment => new {
                            Id = comment.id.ToString(),
                            Author = comment.author.ToString(),
                            DisplayName = displayNameService.GetDisplayName(accountService.Data.GetSingle(comment.author)),
                            Content = comment.content,
                            Timestamp = comment.timestamp
                        }
                    )
                }
            );
        }

        [HttpGet("canpost/{id}"), Authorize]
        public IActionResult GetCanPostComment(string id) {
            CommentThread commentThread = commentThreadService.Data.GetSingle(id);
            Account account = sessionService.GetContextAccount();
            bool admin = sessionService.ContextHasRole(RoleDefinitions.ADMIN);
            bool canPost = commentThread.mode switch {
                ThreadMode.RECRUITER => commentThread.authors.Any(x => x == sessionService.GetContextId()) || admin || recruitmentService.IsRecruiter(sessionService.GetContextAccount()),
                ThreadMode.RANKSUPERIOR => commentThread.authors.Any(x => admin || ranksService.IsSuperior(account.rank, accountService.Data.GetSingle(x).rank)),
                ThreadMode.RANKEQUAL => commentThread.authors.Any(x => admin || ranksService.IsEqual(account.rank, accountService.Data.GetSingle(x).rank)),
                ThreadMode.RANKSUPERIOROREQUAL => commentThread.authors.Any(x => admin || ranksService.IsSuperiorOrEqual(account.rank, accountService.Data.GetSingle(x).rank)),
                _ => true
            };

            return Ok(new {canPost});
        }

        [HttpPut("{id}"), Authorize]
        public async Task<IActionResult> AddComment(string id, [FromBody] Comment comment) {
            comment.id = ObjectId.GenerateNewId().ToString();
            comment.timestamp = DateTime.Now;
            comment.author = sessionService.GetContextId();
            await commentThreadService.InsertComment(id, comment);
            CommentThread thread = commentThreadService.Data.GetSingle(id);
            IEnumerable<string> participants = commentThreadService.GetCommentThreadParticipants(thread.id);
            foreach (string objectId in participants.Where(x => x != comment.author)) {
                notificationsService.Add( // TODO: Set correct link when comment thread is between /application and /recruitment/id
                    new Notification {
                        owner = objectId,
                        icon = NotificationIcons.COMMENT,
                        message = $"{displayNameService.GetDisplayName(comment.author)} replied to a comment:\n\"{comment.content}\"",
                        link = HttpContext.Request.Headers["Referer"].ToString().Replace("http://localhost:4200", "").Replace("https://www.uk-sf.co.uk", "").Replace("https://uk-sf.co.uk", "")
                    }
                );
            }

            return Ok();
        }

        [HttpPost("{id}/{commentId}"), Authorize]
        public async Task<IActionResult> DeleteComment(string id, string commentId) {
            List<Comment> comments = commentThreadService.GetCommentThreadComments(id).ToList();
            Comment comment = comments.FirstOrDefault(x => x.id == commentId);
            await commentThreadService.RemoveComment(id, comment);
            return Ok();
        }
    }
}
