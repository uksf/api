using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionPatchingResultTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        var subject = new MissionPatchingResult();

        subject.PlayerCount.Should().Be(0);
        subject.Success.Should().BeFalse();
        subject.Reports.Should().NotBeNull().And.BeEmpty().And.BeOfType<List<ValidationReport>>();
    }
}
