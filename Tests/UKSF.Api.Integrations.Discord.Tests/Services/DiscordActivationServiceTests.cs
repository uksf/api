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

    private DiscordActivationService CreateSubject(bool canConnect, bool discordFeatureEnabled = true)
    {
        _client.Setup(x => x.CanConnect()).Returns(canConnect);
        _variables.Setup(x => x.GetFeatureState("DISCORD")).Returns(discordFeatureEnabled);
        return new DiscordActivationService(_client.Object, [_discordService.Object], _variables.Object, new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public async Task Activate_WhenTheClientMayNotConnect_NeverConnectsOrActivatesServices()
    {
        await CreateSubject(canConnect: false).Activate();

        _client.Verify(x => x.Connect(), Times.Never);
        _discordService.Verify(x => x.Activate(), Times.Never);
    }

    [Fact]
    public async Task Activate_WhenTheClientMayConnect_ConnectsAndActivatesServices()
    {
        await CreateSubject(canConnect: true).Activate();

        _client.Verify(x => x.Connect(), Times.Once);
        _discordService.Verify(x => x.Activate(), Times.Once);
    }

    [Fact]
    public async Task Activate_WithTheDiscordFeatureOff_StillConnects()
    {
        await CreateSubject(canConnect: true, discordFeatureEnabled: false).Activate();

        _client.Verify(x => x.Connect(), Times.Once);
    }

    [Fact]
    public async Task Deactivate_AfterASkippedActivation_DoesNotDisconnect()
    {
        var subject = CreateSubject(canConnect: false);
        await subject.Activate();

        await subject.Deactivate();

        _client.Verify(x => x.Disconnect(), Times.Never);
    }

    [Fact]
    public async Task Deactivate_AfterAConnectedActivation_Disconnects()
    {
        var subject = CreateSubject(canConnect: true);
        await subject.Activate();

        await subject.Deactivate();

        _client.Verify(x => x.Disconnect(), Times.Once);
    }
}
