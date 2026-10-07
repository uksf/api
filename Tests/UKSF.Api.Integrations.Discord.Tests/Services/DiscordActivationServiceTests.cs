using System.Threading.Tasks;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests.Services;

public class DiscordActivationServiceTests
{
    private readonly Mock<IDiscordClientService> _client = new();
    private readonly Mock<IDiscordService> _discordService = new();
    private readonly Mock<IVariablesService> _variables = new();

    private DiscordActivationService CreateSubject(bool discordEnabled)
    {
        _variables.Setup(x => x.GetFeatureState("DISCORD")).Returns(discordEnabled);
        return new DiscordActivationService(_client.Object, [_discordService.Object], _variables.Object, new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public async Task Activate_WhenDiscordIsDisabled_NeverConnectsOrActivatesServices()
    {
        var subject = CreateSubject(false);

        await subject.Activate();

        _client.Verify(x => x.Connect(), Times.Never);
        _discordService.Verify(x => x.Activate(), Times.Never);
    }

    [Fact]
    public async Task Activate_WhenDiscordIsEnabled_ConnectsAndActivatesServices()
    {
        var subject = CreateSubject(true);

        await subject.Activate();

        _client.Verify(x => x.Connect(), Times.Once);
        _discordService.Verify(x => x.Activate(), Times.Once);
    }

    [Fact]
    public async Task Deactivate_AfterADisabledActivation_DoesNotDisconnect()
    {
        var subject = CreateSubject(false);
        await subject.Activate();

        await subject.Deactivate();

        _client.Verify(x => x.Disconnect(), Times.Never);
    }

    [Fact]
    public async Task Deactivate_AfterAnEnabledActivation_Disconnects()
    {
        var subject = CreateSubject(true);
        await subject.Activate();

        await subject.Deactivate();

        _client.Verify(x => x.Disconnect(), Times.Once);
    }
}
