using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class GitCommandTests
{
    private readonly Mock<IGitService> _mockGitService = new();
    private const string WorkingDirectory = "/test/directory";

    [Fact]
    public async Task Execute_Should_CallGitServiceWithCorrectParameters()
    {
        // Arrange
        const string Command = "status";
        var cancellationToken = CancellationToken.None;
        const string ExpectedResult = "# On branch main";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, cancellationToken)).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory).WithCancellationToken(cancellationToken);

        // Act
        var result = await gitCommand.Execute(Command);

        // Assert
        result.Should().Be(ExpectedResult);
        _mockGitService.Verify(
            x => x.ExecuteCommand(It.Is<GitCommandArgs>(args => args.WorkingDirectory == WorkingDirectory), Command, cancellationToken),
            Times.Once
        );
    }

    [Fact]
    public async Task Execute_Should_CallGitServiceSuccessfully()
    {
        // Arrange
        const string Command = "status";
        const string ExpectedResult = "# On branch main\nnothing to commit";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory);

        // Act
        var result = await gitCommand.Execute(Command);

        // Assert
        result.Should().Be(ExpectedResult);
        _mockGitService.Verify(
            x => x.ExecuteCommand(It.Is<GitCommandArgs>(args => args.WorkingDirectory == WorkingDirectory), Command, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Execute_Should_RethrowOperationCanceledException()
    {
        // Arrange
        const string Command = "status";
        var cancellationException = new OperationCanceledException("Operation was cancelled");

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ThrowsAsync(cancellationException);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => gitCommand.Execute(Command));
        exception.Should().Be(cancellationException);
    }

    [Fact]
    public async Task Execute_Should_RethrowExceptions()
    {
        // Arrange
        const string Command = "invalid-command";
        var originalException = new InvalidOperationException("Git command failed");

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ThrowsAsync(originalException);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => gitCommand.Execute(Command));
        exception.Should().Be(originalException);
    }

    [Fact]
    public async Task WithWorkingDirectory_Should_SetWorkingDirectoryInArgs()
    {
        // Arrange
        const string Command = "status";
        const string CustomWorkingDirectory = "/custom/path";
        const string ExpectedResult = "success";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(CustomWorkingDirectory);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(
            x => x.ExecuteCommand(It.Is<GitCommandArgs>(args => args.WorkingDirectory == CustomWorkingDirectory), Command, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task WithErrorExclusions_Should_SetErrorExclusionsInArgs()
    {
        // Arrange
        const string Command = "status";
        var errorExclusions = new List<string> { "warning", "info" };
        const string ExpectedResult = "success";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory).WithErrorExclusions(errorExclusions);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(
            x => x.ExecuteCommand(
                It.Is<GitCommandArgs>(args => args.ErrorFilter.ErrorExclusions.SequenceEqual(errorExclusions)),
                Command,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task WithAllowedExitCodes_Should_SetAllowedExitCodesInArgs()
    {
        // Arrange
        const string Command = "status";
        var allowedExitCodes = new List<int>
        {
            0,
            1,
            128
        };
        const string ExpectedResult = "success";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory).WithAllowedExitCodes(allowedExitCodes);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(
            x => x.ExecuteCommand(It.Is<GitCommandArgs>(args => args.AllowedExitCodes.SequenceEqual(allowedExitCodes)), Command, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task WithQuiet_Should_SetQuietInArgs()
    {
        // Arrange
        const string Command = "status";
        const string ExpectedResult = "success";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory).WithQuiet(true);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(x => x.ExecuteCommand(It.Is<GitCommandArgs>(args => args.Quiet == true), Command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithCancellationToken_Should_PassCancellationTokenToGitService()
    {
        // Arrange
        const string Command = "status";
        var cancellationToken = new CancellationTokenSource().Token;
        const string ExpectedResult = "success";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, cancellationToken)).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory).WithCancellationToken(cancellationToken);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), Command, cancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("commit -m \"test message\"")]
    [InlineData("push origin main")]
    [InlineData("checkout -b feature-branch")]
    public async Task Execute_Should_PassThroughVariousCommands(string command)
    {
        // Arrange
        var expectedResult = $"Result for: {command}";

        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), command, It.IsAny<CancellationToken>())).ReturnsAsync(expectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object).WithWorkingDirectory(WorkingDirectory);

        // Act
        var result = await gitCommand.Execute(command);

        // Assert
        result.Should().Be(expectedResult);
        _mockGitService.Verify(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
