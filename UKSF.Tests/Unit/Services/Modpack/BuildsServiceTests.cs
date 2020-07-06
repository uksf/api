using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Modpack;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Services.Modpack {
    public class BuildsServiceTests {
        private const string VERSION = "5.17.17";
        private readonly BuildsService buildsService;
        private readonly Mock<IBuildsDataService> mockBuildsDataService;
        private readonly Mock<IBuildStepService> mockBuildStepService;

        public BuildsServiceTests() {
            mockBuildsDataService = new Mock<IBuildsDataService>();
            mockBuildStepService = new Mock<IBuildStepService>();
            buildsService = new BuildsService(mockBuildsDataService.Object, mockBuildStepService.Object);
        }

        [Fact]
        public async Task ShouldCreateAndAddDevBuild() {
            ModpackBuildRelease subject = new ModpackBuildRelease { version = VERSION, builds = new List<ModpackBuild> { new ModpackBuild { buildNumber = 0, isNewVersion = true } } };
            List<ModpackBuildRelease> data = new List<ModpackBuildRelease> { subject };
            GithubCommit commit = new GithubCommit();

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns<Func<ModpackBuildRelease, bool>>(x => data.FirstOrDefault(x));
            mockBuildsDataService.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<ModpackBuild>(), It.IsAny<DataEventType>()))
                                 .Returns(Task.CompletedTask)
                                 .Callback<string, ModpackBuild, DataEventType>((x1, x2, x3) => subject.builds.Add(x2));

            ModpackBuild buildSubject = await buildsService.CreateDevBuild(VERSION, commit);

            subject.builds.Should().HaveCount(2);
            buildSubject.Should().NotBeNull();
            buildSubject.buildNumber.Should().Be(1);
            buildSubject.isNewVersion.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldCreateNewDevBuild() {
            GithubCommit commit = new GithubCommit();
            ModpackBuildRelease subject = null;

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns<Func<ModpackBuildRelease, bool>>(null);
            mockBuildsDataService.Setup(x => x.Add(It.IsAny<ModpackBuildRelease>())).Returns(Task.CompletedTask).Callback<ModpackBuildRelease>(x => subject = x);

            ModpackBuild buildSubject = await buildsService.CreateDevBuild(VERSION, commit);

            subject.Should().NotBeNull();
            subject.version.Should().Be(VERSION);

            subject.builds.Should().HaveCount(1);
            buildSubject.buildNumber.Should().Be(0);
            buildSubject.isNewVersion.Should().BeTrue();
            buildSubject.commit.message.Should().Be("New version (no content changes)");
        }

        [Fact]
        public async Task ShouldThrowForFirstRcBuildWhenNoBuildRelease() {
            ModpackBuild build = new ModpackBuild { buildNumber = 4 };

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns<Func<ModpackBuildRelease, bool>>(null);

            Func<Task> act = async () => await buildsService.CreateFirstRcBuild(VERSION, build);

            await act.Should().ThrowAsync<NullReferenceException>();
        }

        [Fact]
        public async Task ShouldCreateFirstRcBuild() {
            ModpackBuild build = new ModpackBuild { buildNumber = 4 };
            ModpackBuildRelease subject = new ModpackBuildRelease { version = VERSION, builds = new List<ModpackBuild> { build } };

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns(subject);
            mockBuildsDataService.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<ModpackBuild>(), It.IsAny<DataEventType>()))
                                 .Returns(Task.CompletedTask)
                                 .Callback<string, ModpackBuild, DataEventType>((x1, x2, x3) => subject.builds.Add(x2));

            ModpackBuild buildSubject = await buildsService.CreateFirstRcBuild(VERSION, build);

            subject.builds.Should().HaveCount(2);
            subject.builds.Where(x => x.isReleaseCandidate).Should().HaveCount(1);
            buildSubject.buildNumber.Should().Be(5);
            buildSubject.isReleaseCandidate.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldThrowForRcBuildWhenNoBuildRelease() {
            GithubCommit commit = new GithubCommit();

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns<Func<ModpackBuildRelease, bool>>(null);

            Func<Task> act = async () => await buildsService.CreateRcBuild(VERSION, commit);

            await act.Should().ThrowAsync<NullReferenceException>();
        }

        [Fact]
        public async Task ShouldThrowForRcBuildWhenFirstRcBuild() {
            GithubCommit commit = new GithubCommit();
            ModpackBuild build = new ModpackBuild { buildNumber = 4 };
            ModpackBuildRelease subject = new ModpackBuildRelease { version = VERSION, builds = new List<ModpackBuild> { build } };

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns(subject);

            Func<Task> act = async () => await buildsService.CreateRcBuild(VERSION, commit);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task ShouldCreateRcBuild() {
            GithubCommit commit = new GithubCommit();
            ModpackBuild build = new ModpackBuild { buildNumber = 4, isReleaseCandidate = true};
            ModpackBuildRelease subject = new ModpackBuildRelease { version = VERSION, builds = new List<ModpackBuild> { build } };

            mockBuildsDataService.Setup(x => x.GetSingle(It.IsAny<Func<ModpackBuildRelease, bool>>())).Returns(subject);
            mockBuildsDataService.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<ModpackBuild>(), It.IsAny<DataEventType>()))
                                 .Returns(Task.CompletedTask)
                                 .Callback<string, ModpackBuild, DataEventType>((x1, x2, x3) => subject.builds.Add(x2));

            ModpackBuild buildSubject = await buildsService.CreateRcBuild(VERSION, commit);

            subject.builds.Should().HaveCount(2);
            subject.builds.Where(x => x.isReleaseCandidate).Should().HaveCount(2);
            buildSubject.buildNumber.Should().Be(5);
            buildSubject.isReleaseCandidate.Should().BeTrue();
        }
    }
}
