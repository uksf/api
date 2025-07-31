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

public class GitServiceTests
{
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly GitService _gitService;

    public GitServiceTests()
    {
        _mockProcessCommandFactory = new Mock<IProcessCommandFactory>();
        _mockLogger = new Mock<IUksfLogger>();

        _gitService = new GitService(_mockProcessCommandFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public void CreateGitCommand_Should_ReturnGitCommandInstance()
    {
        // Act
        var result = _gitService.CreateGitCommand();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GitCommand>();
    }

    [Fact]
    public async Task ExecuteCommand_WhenOperationCancelledExceptionThrown_Should_RethrowOperationCancelledException()
    {
        // Arrange
        var gitCommandArgs = CreateBasicGitCommandArgs();
        const string command = "status";
        var cancellationToken = new CancellationTokenSource().Token;

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Throws(new OperationCanceledException("Test cancellation"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => _gitService.ExecuteCommand(gitCommandArgs, command, cancellationToken));
        exception.Message.Should().Be("Test cancellation");
    }

    [Fact]
    public async Task ExecuteCommand_WhenGeneralExceptionThrown_Should_LogWarningAndThrowGitOperationException()
    {
        // Arrange
        var gitCommandArgs = CreateBasicGitCommandArgs();
        const string command = "status";
        var originalException = new InvalidOperationException("Test exception");

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws(originalException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _gitService.ExecuteCommand(gitCommandArgs, command));
        exception.Message.Should().Be($"Git operation failed: {command}");
        exception.InnerException.Should().Be(originalException);

        _mockLogger.Verify(x => x.LogWarning($"Git command failed: {command}. Error: {originalException.Message}"), Times.Once);
    }

    private static GitCommandArgs CreateBasicGitCommandArgs()
    {
        return new GitCommandArgs
        {
            WorkingDirectory = "/test/path",
            ErrorFilter = new ErrorFilter(),
            AllowedExitCodes = [],
            Quiet = false
        };
    }
}
