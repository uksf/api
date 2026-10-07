using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests.Services;

public class DiscordClientServiceConnectionTests
{
    private readonly DiscordSocketClient _client = new();

    private DiscordClientService CreateSubject(string environmentName, string verifyFlag = null)
    {
        Mock<IHostEnvironment> environment = new();
        environment.Setup(x => x.EnvironmentName).Returns(environmentName);
        var settings = Options.Create(
            new AppSettings { Secrets = new AppSettings.SecretsConfig { Discord = new AppSettings.SecretsConfig.DiscordConfig { BotToken = "not-a-real-token" } } }
        );
        return new DiscordClientService(settings, _client, new Mock<IVariablesService>().Object, environment.Object, new VerifyMode(verifyFlag, null), new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public void CanConnect_IsTrueOnlyForADeployedInstanceOutsideVerifyMode()
    {
        CreateSubject(Environments.Production).CanConnect().Should().BeTrue();
        CreateSubject(Environments.Development).CanConnect().Should().BeFalse();
        CreateSubject(Environments.Production, "1").CanConnect().Should().BeFalse();
    }

    [Fact]
    public async Task Connect_WhenRunningLocally_NeverLogsIn()
    {
        await CreateSubject(Environments.Development).Connect();

        _client.LoginState.Should().Be(LoginState.LoggedOut);
        _client.ConnectionState.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task Connect_InVerifyMode_NeverLogsIn()
    {
        await CreateSubject(Environments.Production, "1").Connect();

        _client.LoginState.Should().Be(LoginState.LoggedOut);
    }

    [Fact]
    public async Task AssertOnline_WhenRunningLocally_FailsWithoutConnecting()
    {
        var subject = CreateSubject(Environments.Development);

        var act = () => subject.AssertOnline();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Discord*");
        _client.LoginState.Should().Be(LoginState.LoggedOut);
    }
}
