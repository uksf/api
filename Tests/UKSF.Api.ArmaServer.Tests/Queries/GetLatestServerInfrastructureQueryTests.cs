using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Queries;

public class GetLatestServerInfrastructureQueryTests
{
    private readonly Mock<ISteamCmdService> _mockSteamCmdService;
    private readonly GetLatestServerInfrastructureQuery _subject;

    public GetLatestServerInfrastructureQueryTests()
    {
        _mockSteamCmdService = new();

        _subject = new(_mockSteamCmdService.Object);
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam()
    {
        Given_steam_cmd_returns_info();

        var result = await _subject.ExecuteAsync();

        result.LatestBuild.Should().Be("7513259");
        result.LatestUpdate.Should().Be(new(2021, 10, 11, 12, 55, 23));
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_bad_info()
    {
        Given_steam_cmd_returns_bad_info();

        Func<Task> act = async () => await _subject.ExecuteAsync();

        await act.Should().ThrowAsync<ServerInfrastructureException>().WithMessageAndStatusCode("No build info found in Steam data", 404);
    }

    [Fact]
    public async Task When_getting_latest_server_info_from_steam_with_no_info()
    {
        Given_steam_cmd_returns_no_info();

        Func<Task> act = async () => await _subject.ExecuteAsync();

        await act.Should().ThrowAsync<ServerInfrastructureException>().WithMessageAndStatusCode("No info found from Steam", 404);
    }

    private void Given_steam_cmd_returns_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdInfo.txt"));
    }

    private void Given_steam_cmd_returns_no_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdNoInfo.txt"));
    }

    private void Given_steam_cmd_returns_bad_info()
    {
        _mockSteamCmdService.Setup(x => x.GetServerInfo()).ReturnsAsync(File.ReadAllText("TestData/SteamCmdBadInfo.txt"));
    }
}
