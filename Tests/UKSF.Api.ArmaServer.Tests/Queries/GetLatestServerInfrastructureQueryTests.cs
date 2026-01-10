using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Queries;

public class GetLatestServerInfrastructureQueryTests
{
    private readonly Mock<IOptions<AppSettings>> _mockAppSettings = new();
    private readonly Mock<IUksfLogger> _mockLoggingService = new();
    private readonly Mock<ISteamCmdService> _mockSteamCmdService = new();
    private readonly GetLatestServerInfrastructureQuery _subject;

    public GetLatestServerInfrastructureQueryTests()
    {
        _subject = new GetLatestServerInfrastructureQuery(_mockSteamCmdService.Object, _mockAppSettings.Object, _mockLoggingService.Object);
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam()
    {
        Given_steam_cmd_returns_info();

        var result = await _subject.ExecuteAsync(0);

        result.LatestBuild.Should().Be("12403383");
        result.LatestUpdate.Should().Be(new DateTime(2023, 10, 10, 12, 42, 30));
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_bad_info()
    {
        Given_steam_cmd_returns_bad_info();

        var act = () => _subject.ExecuteAsync(0);

        await act.Should().ThrowAsync<ServerInfrastructureException>().WithMessageAndStatusCode("No build info found in Steam data", 404);
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_failed_info()
    {
        Given_steam_cmd_returns_failed_info();

        var act = () => _subject.ExecuteAsync(0);

        await act.Should().ThrowAsync<ServerInfrastructureException>().WithMessageAndStatusCode("No info found from Steam", 404);
        _mockSteamCmdService.Verify(x => x.GetServerInfo(), Times.Exactly(10));
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_failed_info_then_valid_info()
    {
        var failedInfo = await File.ReadAllTextAsync("TestData/SteamCmdFailedInfo.txt");
        var validInfo = await File.ReadAllTextAsync("TestData/SteamCmdInfo.txt");
        _mockSteamCmdService.SetupSequence(x => x.GetServerInfo())
                            .ReturnsAsync(failedInfo)
                            .ReturnsAsync(failedInfo)
                            .ReturnsAsync(failedInfo)
                            .ReturnsAsync(failedInfo)
                            .ReturnsAsync(failedInfo)
                            .ReturnsAsync(validInfo);

        var result = await _subject.ExecuteAsync(0);

        result.LatestBuild.Should().Be("12403383");
        result.LatestUpdate.Should().Be(new DateTime(2023, 10, 10, 12, 42, 30));
        _mockSteamCmdService.Verify(x => x.GetServerInfo(), Times.Exactly(6));
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_info_with_mixed_outputs()
    {
        Given_steam_cmd_returns_info_with_mixed_outputs();

        var result = await _subject.ExecuteAsync(0);

        result.LatestBuild.Should().Be("12403383");
        result.LatestUpdate.Should().Be(new DateTime(2023, 10, 10, 12, 42, 30));
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_no_info()
    {
        Given_steam_cmd_returns_no_info();

        var act = () => _subject.ExecuteAsync(0);

        await act.Should().ThrowAsync<ServerInfrastructureException>().WithMessageAndStatusCode("No info found from Steam", 404);
    }

    private void Given_steam_cmd_returns_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdInfo.txt"));
    }

    private void Given_steam_cmd_returns_failed_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdFailedInfo.txt"));
    }

    private void Given_steam_cmd_returns_no_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdNoInfo.txt"));
    }

    private void Given_steam_cmd_returns_bad_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdBadInfo.txt"));
    }

    private void Given_steam_cmd_returns_info_with_mixed_outputs()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdInfoMixedOutputs.txt"));
    }
}
