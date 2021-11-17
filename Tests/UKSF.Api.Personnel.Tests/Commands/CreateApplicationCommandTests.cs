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
        private readonly string _accountId = "id";
        private readonly string _discordId = "discord";
        private readonly Mock<IAccountContext> _mockAccountContext;
        private readonly Mock<IAssignmentService> _mockAssignmentService;
        private readonly Mock<ICommentThreadContext> _mockCommentThreadContext;
        private readonly Mock<ICommentThreadService> _mockCommentThreadService;
        private readonly Mock<IDisplayNameService> _mockDisplayNameService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<INotificationsService> _mockNotificationsService;
        private readonly Mock<IRecruitmentService> _mockRecruitmentService;
        private readonly string _steamName = "steam";
        private readonly CreateApplicationCommand _subject;

        public CreateApplicationCommandTests()
        {
            _mockAccountContext = new();
            _mockAssignmentService = new();
            _mockCommentThreadContext = new();
            _mockDisplayNameService = new();
            _mockCommentThreadService = new();
            _mockLogger = new();
            _mockNotificationsService = new();
            _mockRecruitmentService = new();

            _mockRecruitmentService.Setup(x => x.GetRecruiterLeads()).Returns(new Dictionary<string, string>());
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Id == _accountId))).Returns("Last.F");

            _subject = new(
                _mockAccountContext.Object,
                _mockCommentThreadContext.Object,
                _mockRecruitmentService.Object,
                _mockAssignmentService.Object,
                _mockNotificationsService.Object,
                _mockDisplayNameService.Object,
                _mockCommentThreadService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task When_creating_application_with_existing_steam_and_discord_connection()
        {
            Given_an_account_with_application();
            _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                               .Returns(
                                   new List<DomainAccount> { new() { Lastname = "Match", Firstname = "1", Steamname = _steamName, DiscordId = _discordId } }
                               );
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Lastname == "Match"))).Returns("Match.1");

            await _subject.ExecuteAsync(_accountId, new());

            _mockCommentThreadService.Verify(
                x => x.InsertComment(It.IsAny<string>(), It.Is<Comment>(m => m.Content == "Last.F has the same Steam account as Match.1")),
                Times.Once
            );
            _mockCommentThreadService.Verify(
                x => x.InsertComment(It.IsAny<string>(), It.Is<Comment>(m => m.Content == "Last.F has the same Discord account as Match.1")),
                Times.Once
            );
        }

        [Fact]
        public async Task When_creating_application_with_multiple_existing_steam_and_discord_connections()
        {
            Given_an_account_with_application();
            _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                               .Returns(
                                   new List<DomainAccount>
                                   {
                                       new() { Lastname = "Match", Firstname = "1", Steamname = _steamName, DiscordId = _discordId },
                                       new() { Lastname = "Match", Firstname = "2", Steamname = _steamName, DiscordId = _discordId }
                                   }
                               );
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Firstname == "1"))).Returns("Match.1");
            _mockDisplayNameService.Setup(x => x.GetDisplayNameWithoutRank(It.Is<DomainAccount>(m => m.Firstname == "2"))).Returns("Match.2");

            await _subject.ExecuteAsync(_accountId, new());

            _mockCommentThreadService.Verify(
                x => x.InsertComment(It.IsAny<string>(), It.Is<Comment>(m => m.Content == "Last.F has the same Steam account as Match.1, Match.2")),
                Times.Once
            );
            _mockCommentThreadService.Verify(
                x => x.InsertComment(It.IsAny<string>(), It.Is<Comment>(m => m.Content == "Last.F has the same Discord account as Match.1, Match.2")),
                Times.Once
            );
        }

        [Fact]
        public async Task When_creating_application_with_no_existing_steam_and_discord_connections()
        {
            Given_an_account_with_application();
            _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());

            await _subject.ExecuteAsync(_accountId, new());

            _mockCommentThreadService.Verify(x => x.InsertComment(It.IsAny<string>(), It.IsAny<Comment>()), Times.Never);
        }

        private void Given_an_account_with_application()
        {
            _mockAccountContext.Setup(x => x.GetSingle(_accountId))
                               .Returns(
                                   () => new()
                                   {
                                       Id = _accountId,
                                       Lastname = "Last",
                                       Firstname = "First",
                                       Application = new(),
                                       Steamname = _steamName,
                                       DiscordId = _discordId
                                   }
                               );
        }
    }
}
