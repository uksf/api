using System.IO;
using FluentAssertions;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Models;

public class MissionFileTests
{
    [Fact]
    public void ShouldSetFields()
    {
        MissionFile subject = new(new FileInfo("TestData/testmission.Altis.pbo"));

        subject.Path.Should().Be("testmission.Altis.pbo");
        subject.Map.Should().Be("Altis");
        subject.Name.Should().Be("testmission");
    }
}
