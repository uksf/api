using System.IO;
using FluentAssertions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Admin;

public class VerifyModeTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Enabled_FollowsTheFlag(string? flag, bool expected)
    {
        new VerifyMode(flag, null).Enabled.Should().Be(expected);
    }

    [Fact]
    public void EmailDirectory_DefaultsUnderTheTempPath()
    {
        new VerifyMode("1", null).EmailDirectory.Should().Be(Path.Combine(Path.GetTempPath(), "uksf-verify-email"));
    }

    [Fact]
    public void EmailDirectory_UsesTheConfiguredPath()
    {
        new VerifyMode("1", "/srv/verify/mail").EmailDirectory.Should().Be("/srv/verify/mail");
    }
}
