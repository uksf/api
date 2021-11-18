using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.Personnel.Commands;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using Xunit;

namespace UKSF.Api.Personnel.Tests.Commands
{
    public class CreateApplicationCommandTests
    {
        private const string ACCOUNT_ID = "id";
        private const string APPLICATION_COMMENT_THREAD_ID = "applicationCommentThreadId";
        private const string DISCORD_ID = "discord";
        private const string RECRUITER_COMMENT_THREAD_ID = "recruiterCommentThreadId";
        private const string STEAM_NAME = "steam";
        private readonly Mock<IAccountContext> _mockAccountContext;
        private readonly Mock<IAssignmentService> _mockAssignmentService;
        private readonly Mock<ICommentThreadService> _mockCommentThreadService;
        private readonly Mock<ICreateCommentThreadCommand> _mockCreateCommentThreadCommand;
        private readonly Mock<IDisplayNameService> _mockDisplayNameService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<INotificationsService> _mockNotificationsService;
        private readonly Mock<IRecruitmentService> _mockRecruitmentService;
        private readonly CreateApplicationCommand _subject;

        public CreateApplicationCommandTests()
        {
            _mockAccountContext = new();
            _mockAssignmentService = new();
            _mockDisplayNameService = new();
            _mockCommentThreadService = new();
            _mockLogger = new();
            _mockNotificationsService = new();
            _mockRecruitmentService = new();
            _mockCreateCommentThreadCommand = new();

            _mockRecruitmentService.Setup(x => x.GetRecruiterLeads()).Returns(new Dictionary<string, string>());
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Id == ACCOUNT_ID))).Returns("Last.F");

            _subject = new(
                _mockAccountContext.Object,
                _mockRecruitmentService.Object,
                _mockAssignmentService.Object,
                _mockNotificationsService.Object,
                _mockDisplayNameService.Object,
                _mockCommentThreadService.Object,
                _mockCreateCommentThreadCommand.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task When_creating_application_with_existing_steam_and_discord_connection()
        {
            Given_application_comment_threads();
            Given_an_account_with_application();
            _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                               .Returns(
                                   new List<DomainAccount>
                                   {
                                       new()
                                       {
                                           Lastname = "Match",
                                           Firstname = "1",
                                           Steamname = STEAM_NAME,
                                           DiscordId = DISCORD_ID,
                                           MembershipState = MembershipState.MEMBER
                                       }
                                   }
                               );
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Lastname == "Match"))).Returns("Match.1");

            await _subject.ExecuteAsync(ACCOUNT_ID, new());

            _mockCommentThreadService.Verify(
                x => x.InsertComment(RECRUITER_COMMENT_THREAD_ID, It.Is<Comment>(m => m.Content == "Last.F has the same Steam account as Match.1")),
                Times.Once
            );
            _mockCommentThreadService.Verify(
                x => x.InsertComment(RECRUITER_COMMENT_THREAD_ID, It.Is<Comment>(m => m.Content == "Last.F has the same Discord account as Match.1")),
                Times.Once
            );
        }

        [Fact]
        public async Task When_creating_application_with_multiple_existing_steam_and_discord_connections()
        {
            Given_application_comment_threads();
            Given_an_account_with_application();
            _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                               .Returns(
                                   new List<DomainAccount>
                                   {
                                       new()
                                       {
                                           Lastname = "Match",
                                           Firstname = "1",
                                           Steamname = STEAM_NAME,
                                           DiscordId = DISCORD_ID,
                                           MembershipState = MembershipState.CONFIRMED
                                       },
                                       new()
                                       {
                                           Lastname = "Match",
                                           Firstname = "2",
                                           Steamname = STEAM_NAME,
                                           DiscordId = DISCORD_ID,
                                           MembershipState = MembershipState.DISCHARGED
                                       }
                                   }
                               );
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Firstname == "1"))).Returns("Match.1");
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Firstname == "2"))).Returns("Match.2");

            await _subject.ExecuteAsync(ACCOUNT_ID, new());

            _mockCommentThreadService.Verify(
                x => x.InsertComment(RECRUITER_COMMENT_THREAD_ID, It.Is<Comment>(m => m.Content == "Last.F has the same Steam account as Match.1, Match.2")),
                Times.Once
            );
            _mockCommentThreadService.Verify(
                x => x.InsertComment(RECRUITER_COMMENT_THREAD_ID, It.Is<Comment>(m => m.Content == "Last.F has the same Discord account as Match.1, Match.2")),
                Times.Once
            );
        }

        [Fact]
        public async Task When_creating_application_with_no_existing_steam_and_discord_connections()
        {
            Given_application_comment_threads();
            Given_an_account_with_application();
            _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

            await _subject.ExecuteAsync(ACCOUNT_ID, new());

            _mockCommentThreadService.Verify(x => x.InsertComment(RECRUITER_COMMENT_THREAD_ID, It.IsAny<Comment>()), Times.Never);
        }

        private void Given_application_comment_threads()
        {
            _mockCreateCommentThreadCommand.Setup(x => x.ExecuteAsync(It.Is<string[]>(m => m.Length == 0), ThreadMode.RECRUITER))
                                           .ReturnsAsync(new CommentThread { Id = RECRUITER_COMMENT_THREAD_ID });
            _mockCreateCommentThreadCommand.Setup(x => x.ExecuteAsync(It.Is<string[]>(m => m.Length == 1), ThreadMode.RECRUITER))
                                           .ReturnsAsync(new CommentThread { Id = APPLICATION_COMMENT_THREAD_ID });
        }

        private void Given_an_account_with_application()
        {
            _mockAccountContext.Setup(x => x.GetSingle(ACCOUNT_ID))
                               .Returns(
                                   () => new()
                                   {
                                       Id = ACCOUNT_ID,
                                       Lastname = "Last",
                                       Firstname = "First",
                                       Application = new()
                                       {
                                           RecruiterCommentThread = RECRUITER_COMMENT_THREAD_ID, ApplicationCommentThread = APPLICATION_COMMENT_THREAD_ID
                                       },
                                       Steamname = STEAM_NAME,
                                       DiscordId = DISCORD_ID
                                   }
                               );
        }
    }
}
