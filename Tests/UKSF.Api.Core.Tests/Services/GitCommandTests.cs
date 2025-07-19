using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
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
        const bool IgnoreErrors = false;
        var cancellationToken = CancellationToken.None;
        const string ExpectedResult = "# On branch main";

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, cancellationToken)).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act
        await gitCommand.Execute(Command, IgnoreErrors, cancellationToken);

        // Assert
        _mockGitService.Verify(x => x.ExecuteCommand(WorkingDirectory, Command, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Execute_Should_CallGitServiceSuccessfully()
    {
        // Arrange
        const string Command = "status";
        const string ExpectedResult = "# On branch main\nnothing to commit";

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_Should_RethrowOperationCanceledException_RegardlessOfIgnoreErrors()
    {
        // Arrange
        const string Command = "status";
        var cancellationException = new OperationCanceledException("Operation was cancelled");

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>())).ThrowsAsync(cancellationException);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act & Assert - Should rethrow even with ignoreErrors = true
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => gitCommand.Execute(Command, ignoreErrors: true));

        exception.Should().Be(cancellationException);
    }

    [Fact]
    public async Task Execute_Should_RethrowException_WhenIgnoreErrorsIsFalse()
    {
        // Arrange
        const string Command = "invalid-command";
        var originalException = new InvalidOperationException("Git command failed");

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>())).ThrowsAsync(originalException);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => gitCommand.Execute(Command, ignoreErrors: false));

        exception.Should().Be(originalException);
    }

    [Fact]
    public async Task Execute_Should_SwallowException_WhenIgnoreErrorsIsTrue()
    {
        // Arrange
        const string Command = "invalid-command";
        var originalException = new InvalidOperationException("Git command failed");

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>())).ThrowsAsync(originalException);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act - Should not throw when ignoreErrors is true
        await gitCommand.Execute(Command, ignoreErrors: true);

        // Assert - Method completed without throwing
        _mockGitService.Verify(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_Should_UseDefaultIgnoreErrorsValue()
    {
        // Arrange
        const string Command = "invalid-command";
        var originalException = new InvalidOperationException("Git command failed");

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>())).ThrowsAsync(originalException);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act & Assert - Default ignoreErrors should be false
        await Assert.ThrowsAsync<InvalidOperationException>(() => gitCommand.Execute(Command));
    }

    [Fact]
    public async Task Execute_Should_UseDefaultCancellationToken()
    {
        // Arrange
        const string Command = "status";
        const string ExpectedResult = "# On branch main";

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, Command, It.IsAny<CancellationToken>())).ReturnsAsync(ExpectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act
        await gitCommand.Execute(Command);

        // Assert
        _mockGitService.Verify(x => x.ExecuteCommand(WorkingDirectory, Command, It.Is<CancellationToken>(ct => ct == CancellationToken.None)), Times.Once);
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

        _mockGitService.Setup(x => x.ExecuteCommand(WorkingDirectory, command, It.IsAny<CancellationToken>())).ReturnsAsync(expectedResult);

        var gitCommand = new GitCommand(_mockGitService.Object, WorkingDirectory);

        // Act
        await gitCommand.Execute(command);

        // Assert
        _mockGitService.Verify(x => x.ExecuteCommand(WorkingDirectory, command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
