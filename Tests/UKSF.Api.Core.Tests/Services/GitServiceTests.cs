using FluentAssertions;
using Moq;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class GitServiceTests
{
    private readonly GitService _gitService;

    public GitServiceTests()
    {
        var mockProcessCommandFactory = new Mock<IProcessCommandFactory>();
        var mockLogger = new Mock<IUksfLogger>();

        _gitService = new GitService(mockProcessCommandFactory.Object, mockLogger.Object);
    }

    [Fact]
    public void CreateGitCommand_Should_ReturnGitCommandInstance()
    {
        // Arrange
        const string WorkingDirectory = "/test/directory";

        // Act
        var result = _gitService.CreateGitCommand(WorkingDirectory);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GitCommand>();
    }
}
