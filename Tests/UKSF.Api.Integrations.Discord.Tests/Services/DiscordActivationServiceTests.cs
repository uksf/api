using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
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

    private DiscordActivationService CreateSubject(string environmentName, bool discordFeatureEnabled = true)
    {
        _variables.Setup(x => x.GetFeatureState("DISCORD")).Returns(discordFeatureEnabled);
        Mock<IHostEnvironment> environment = new();
        environment.Setup(x => x.EnvironmentName).Returns(environmentName);
        return new DiscordActivationService(_client.Object, [_discordService.Object], _variables.Object, environment.Object, new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public async Task Activate_WhenRunningLocally_NeverConnectsOrActivatesServices()
    {
        var subject = CreateSubject(Environments.Development);

        await subject.Activate();

        _client.Verify(x => x.Connect(), Times.Never);
        _discordService.Verify(x => x.Activate(), Times.Never);
    }

    [Fact]
    public async Task Activate_WhenDeployed_ConnectsAndActivatesServices()
    {
        var subject = CreateSubject(Environments.Production);

        await subject.Activate();

        _client.Verify(x => x.Connect(), Times.Once);
        _discordService.Verify(x => x.Activate(), Times.Once);
    }

    [Fact]
    public async Task Activate_WhenDeployedWithTheDiscordFeatureOff_StillConnects()
    {
        var subject = CreateSubject(Environments.Production, discordFeatureEnabled: false);

        await subject.Activate();

        _client.Verify(x => x.Connect(), Times.Once);
    }

    [Fact]
    public async Task Deactivate_AfterALocalActivation_DoesNotDisconnect()
    {
        var subject = CreateSubject(Environments.Development);
        await subject.Activate();

        await subject.Deactivate();

        _client.Verify(x => x.Disconnect(), Times.Never);
    }

    [Fact]
    public async Task Deactivate_AfterADeployedActivation_Disconnects()
    {
        var subject = CreateSubject(Environments.Production);
        await subject.Activate();

        await subject.Deactivate();

        _client.Verify(x => x.Disconnect(), Times.Once);
    }
}
