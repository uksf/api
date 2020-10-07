using System.IO;
using FluentAssertions;
using UKSF.Api.Models.Game;
using Xunit;

namespace UKSF.Tests.Unit.Models.Game {
    public class MissionFileTests {
        [Fact]
        public void ShouldSetFields() {
            MissionFile subject = new MissionFile(new FileInfo("../../../testdata/testmission.Altis.pbo"));

            subject.path.Should().Be("testmission.Altis.pbo");
            subject.map.Should().Be("Altis");
            subject.name.Should().Be("testmission");
        }
    }
}
