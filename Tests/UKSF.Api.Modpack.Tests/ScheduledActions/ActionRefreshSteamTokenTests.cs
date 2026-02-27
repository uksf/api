using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.ScheduledActions;
using Xunit;

namespace UKSF.Api.Modpack.Tests.ScheduledActions;

public class ActionRefreshSteamTokenTests
{
    private readonly Mock<IClock> _mockClock = new();
    private readonly Mock<IHostEnvironment> _mockEnvironment = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<ISchedulerService> _mockSchedulerService = new();
    private readonly Mock<ISteamCmdService> _mockSteamCmdService = new();
    private readonly ActionRefreshSteamToken _action;

    public ActionRefreshSteamTokenTests()
    {
        _action = new ActionRefreshSteamToken(
            _mockSchedulerService.Object,
            _mockEnvironment.Object,
            _mockClock.Object,
            _mockSteamCmdService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Name_ShouldBeActionRefreshSteamToken()
    {
        _action.Name.Should().Be("ActionRefreshSteamToken");
    }

    [Fact]
    public void RunInterval_ShouldBe12Hours()
    {
        _action.RunInterval.Should().Be(TimeSpan.FromHours(12));
    }

    [Fact]
    public void NextRun_ShouldBeToday6Am()
    {
        var today = new DateTime(2026, 2, 27);
        _mockClock.Setup(x => x.UkToday()).Returns(today);

        _action.NextRun.Should().Be(today.AddHours(6));
    }

    [Fact]
    public async Task Run_WhenLoginSucceeds_ShouldLogSuccess()
    {
        _mockSteamCmdService.Setup(x => x.RefreshLogin()).ReturnsAsync("Logged in OK\nWaiting for user info...\nOK");

        await _action.Run();

        _mockSteamCmdService.Verify(x => x.RefreshLogin(), Times.Once);
        _mockLogger.Verify(x => x.LogInfo("Steam token refreshed successfully"), Times.Once);
    }

    [Fact]
    public async Task Run_WhenOutputContainsSteamGuard_ShouldThrowAndLogError()
    {
        var output =
            "Logging in user 'test' to Steam Public...\nThis computer has not been authenticated for your account using Steam Guard.\nSteam Guard code:\nERROR (Account Logon Denied)";
        _mockSteamCmdService.Setup(x => x.RefreshLogin()).ReturnsAsync(output);

        var act = () => _action.Run();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Steam Guard*manual re-authentication*");
        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Steam Guard code required") && s.Contains(output))), Times.Once);
    }

    [Fact]
    public async Task Run_WhenOutputContainsAccountLogonDenied_ShouldThrowAndLogError()
    {
        var output = "Account Logon Denied";
        _mockSteamCmdService.Setup(x => x.RefreshLogin()).ReturnsAsync(output);

        var act = () => _action.Run();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Steam Guard*manual re-authentication*");
    }

    [Fact]
    public async Task Run_WhenOutputContainsLoginFailure_ShouldThrowAndLogError()
    {
        var output = "Login Failure: Invalid Password";
        _mockSteamCmdService.Setup(x => x.RefreshLogin()).ReturnsAsync(output);

        var act = () => _action.Run();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Steam login failed*");
        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Steam login failed") && s.Contains(output))), Times.Once);
    }

    [Fact]
    public async Task Run_WhenOutputContainsFailed_ShouldThrowAndLogError()
    {
        var output = "FAILED to login";
        _mockSteamCmdService.Setup(x => x.RefreshLogin()).ReturnsAsync(output);

        var act = () => _action.Run();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Steam login failed*");
    }

    [Fact]
    public async Task Run_WhenRefreshThrowsException_ShouldLogErrorAndRethrow()
    {
        var exception = new Exception("SteamCMD process crashed");
        _mockSteamCmdService.Setup(x => x.RefreshLogin()).ThrowsAsync(exception);

        var act = () => _action.Run();

        await act.Should().ThrowAsync<Exception>().WithMessage("SteamCMD process crashed");
        _mockLogger.Verify(x => x.LogError("Failed to refresh Steam token", exception), Times.Once);
    }
}
