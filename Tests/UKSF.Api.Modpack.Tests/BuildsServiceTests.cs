using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Tests;

public class BuildsServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IBuildsContext> _mockBuildsContext;
    private readonly Mock<IBuildStepService> _mockBuildStepService;
    private readonly BuildsService _subject;

    public BuildsServiceTests()
    {
        _mockBuildsContext = new();
        _mockBuildStepService = new();
        _mockAccountContext = new();
        Mock<IHttpContextService> mockHttpContextService = new();
        Mock<IUksfLogger> mockLogger = new();

        _subject = new(_mockBuildsContext.Object, _mockBuildStepService.Object, _mockAccountContext.Object, mockHttpContextService.Object, mockLogger.Object);
    }

    // [Fact]
    // public async Task When_creating_rc_build()
    // {
    //     _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<ModpackBuild, bool>>())).Returns(new List<ModpackBuild>());
    //     _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(new DomainAccount { Id = "accountId" });
    //     _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.RC)).Returns(new List<ModpackBuildStep>());
    //
    //     GithubCommit githubCommit = new() { Author = "author" };
    //     ModpackBuild result = await _subject.CreateRcBuild("1.1.0", githubCommit);
    //
    //     result.Environment.Should().Be(GameEnvironment.RC);
    //     result.BuildNumber.Should().Be(1);
    //     result.BuilderId.Should().Be("accountId");
    //     result.EnvironmentVariables.Should().BeEquivalentTo(new Dictionary<string, object> { { "ace_updated", true }, { "acre_updated", true }, { "uksf_air_updated", true } });
    // }
}
