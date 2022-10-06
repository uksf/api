using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;
using Xunit;

namespace UKSF.Api.Tests.Commands;

public class CreateApplicationCommandTests
{
    private const string AccountId = "id";
    private const string ApplicationCommentThreadId = "applicationCommentThreadId";
    private const string DiscordId = "discord";
    private const string RecruiterCommentThreadId = "recruiterCommentThreadId";
    private const string SteamName = "steam";
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IAssignmentService> _mockAssignmentService;
    private readonly Mock<ICommentThreadService> _mockCommentThreadService;
    private readonly Mock<ICreateCommentThreadCommand> _mockCreateCommentThreadCommand;
    private readonly Mock<IDisplayNameService> _mockDisplayNameService;
    private readonly Mock<IUksfLogger> _mockLogger;
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
        _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Id == AccountId))).Returns("Last.F");

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
                                       Steamname = SteamName,
                                       DiscordId = DiscordId,
                                       MembershipState = MembershipState.MEMBER
                                   }
                               }
                           );
        _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Lastname == "Match"))).Returns("Match.1");

        await _subject.ExecuteAsync(AccountId, new());

        _mockCommentThreadService.Verify(
            x => x.InsertComment(RecruiterCommentThreadId, It.Is<Comment>(m => m.Content == "Last.F has the same Steam account as Match.1")),
            Times.Once
        );
        _mockCommentThreadService.Verify(
            x => x.InsertComment(RecruiterCommentThreadId, It.Is<Comment>(m => m.Content == "Last.F has the same Discord account as Match.1")),
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
                                       Steamname = SteamName,
                                       DiscordId = DiscordId,
                                       MembershipState = MembershipState.CONFIRMED
                                   },
                                   new()
                                   {
                                       Lastname = "Match",
                                       Firstname = "2",
                                       Steamname = SteamName,
                                       DiscordId = DiscordId,
                                       MembershipState = MembershipState.DISCHARGED
                                   }
                               }
                           );
        _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Firstname == "1"))).Returns("Match.1");
        _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Firstname == "2"))).Returns("Match.2");

        await _subject.ExecuteAsync(AccountId, new());

        _mockCommentThreadService.Verify(
            x => x.InsertComment(RecruiterCommentThreadId, It.Is<Comment>(m => m.Content == "Last.F has the same Steam account as Match.1, Match.2")),
            Times.Once
        );
        _mockCommentThreadService.Verify(
            x => x.InsertComment(RecruiterCommentThreadId, It.Is<Comment>(m => m.Content == "Last.F has the same Discord account as Match.1, Match.2")),
            Times.Once
        );
    }

    [Fact]
    public async Task When_creating_application_with_no_existing_steam_and_discord_connections()
    {
        Given_application_comment_threads();
        Given_an_account_with_application();
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

        await _subject.ExecuteAsync(AccountId, new());

        _mockCommentThreadService.Verify(x => x.InsertComment(RecruiterCommentThreadId, It.IsAny<Comment>()), Times.Never);
    }

    private void Given_application_comment_threads()
    {
        _mockCreateCommentThreadCommand.Setup(x => x.ExecuteAsync(It.Is<string[]>(m => m.Length == 0), ThreadMode.RECRUITER))
                                       .ReturnsAsync(new CommentThread { Id = RecruiterCommentThreadId });
        _mockCreateCommentThreadCommand.Setup(x => x.ExecuteAsync(It.Is<string[]>(m => m.Length == 1), ThreadMode.RECRUITER))
                                       .ReturnsAsync(new CommentThread { Id = ApplicationCommentThreadId });
    }

    private void Given_an_account_with_application()
    {
        _mockAccountContext.Setup(x => x.GetSingle(AccountId))
                           .Returns(
                               () => new()
                               {
                                   Id = AccountId,
                                   Lastname = "Last",
                                   Firstname = "First",
                                   Application = new()
                                   {
                                       RecruiterCommentThread = RecruiterCommentThreadId, ApplicationCommentThread = ApplicationCommentThreadId
                                   },
                                   Steamname = SteamName,
                                   DiscordId = DiscordId
                               }
                           );
    }
}
