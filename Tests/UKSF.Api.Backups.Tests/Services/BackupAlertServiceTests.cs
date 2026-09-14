using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupAlertServiceTests
{
    private const ulong DefaultChannel = 707615025380065400;

    private readonly Mock<IDiscordMessageService> _mockDiscordMessageService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly BackupAlertService _subject;

    public BackupAlertServiceTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable(It.IsAny<string>())).Returns((string key) => new DomainVariableItem { Key = key });
        _subject = new BackupAlertService(_mockVariablesService.Object, _mockDiscordMessageService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task An_alert_posts_to_the_backups_channel_and_logs()
    {
        await _subject.Alert("run failed: mongodump failed");

        _mockDiscordMessageService.Verify(x => x.SendMessage(DefaultChannel, It.Is<string>(y => y.Contains("mongodump failed"))), Times.Once);
        _mockLogger.Verify(x => x.LogError(It.Is<string>(y => y.Contains("mongodump failed"))), Times.Once);
    }

    [Fact]
    public async Task The_channel_can_be_moved_with_a_variable()
    {
        _mockVariablesService.Setup(x => x.GetVariable("DID_C_BACKUPS")).Returns(new DomainVariableItem { Key = "DID_C_BACKUPS", Item = "123456789" });

        await _subject.Alert("run failed");

        _mockDiscordMessageService.Verify(x => x.SendMessage(123456789UL, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task A_discord_outage_does_not_swallow_the_alert()
    {
        _mockDiscordMessageService.Setup(x => x.SendMessage(It.IsAny<ulong>(), It.IsAny<string>())).ThrowsAsync(new Exception("gateway down"));

        var act = () => _subject.Alert("run failed: disk full");

        await act.Should().NotThrowAsync();
        _mockLogger.Verify(x => x.LogError(It.Is<string>(y => y.Contains("disk full"))), Times.Once);
        _mockLogger.Verify(x => x.LogError(It.Is<string>(y => y.Contains("could not be sent to Discord"))), Times.Once);
    }
}
