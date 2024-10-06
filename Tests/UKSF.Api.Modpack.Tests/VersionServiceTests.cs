using FluentAssertions;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class VersionServiceTests
{
    private readonly VersionService _subject = new();

    [Theory]
    [InlineData("1.0.1", "1.0.0")]
    [InlineData("1.1.0", "1.0.0")]
    [InlineData("2.0.0", "1.0.0")]
    [InlineData("1.10.0", "1.9.10")]
    [InlineData("1.9.11", "1.9.10")]
    [InlineData("10.0.0", "9.9.9")]
    [InlineData("1.0.0", "0.0.0")]
    [InlineData("1.0.0", "0.0.1")]
    [InlineData("1.0.0", "0.1.0")]
    public void VersionShouldBeIncremental(string version, string previousVersion)
    {
        var result = _subject.IsVersionIncremental(version, previousVersion);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("3.0.0", "1.0.0")]
    [InlineData("2.1.0", "1.0.0")]
    [InlineData("1.1.1", "1.0.0")]
    [InlineData("0.0.3", "0.0.1")]
    [InlineData("0.2.0", "0.0.1")]
    [InlineData("2.0.0", "0.0.1")]
    [InlineData("0.2.1", "0.1.1")]
    [InlineData("1.1.1", "0.1.1")]
    public void VersionShouldNotBeIncremental(string version, string previousVersion)
    {
        var result = _subject.IsVersionIncremental(version, previousVersion);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("2.0.0", "1.0.0")]
    [InlineData("1.0.1", "1.0.0")]
    [InlineData("1.1.0", "1.0.0")]
    [InlineData("2.0.0", "1.0.1")]
    [InlineData("1.1.0", "1.0.1")]
    [InlineData("1.0.2", "1.0.1")]
    [InlineData("2.0.0", "1.1.1")]
    [InlineData("1.2.0", "1.1.1")]
    [InlineData("1.1.2", "1.1.1")]
    [InlineData("3.0.0", "1.1.1")]
    [InlineData("1.3.0", "1.1.1")]
    [InlineData("1.1.3", "1.1.1")]
    public void VersionShouldBeNewer(string version, string previousVersion)
    {
        var result = _subject.IsVersionNewer(version, previousVersion);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("0.0.0", "1.0.0")]
    [InlineData("0.10.10", "1.0.0")]
    [InlineData("1.0.2", "2.0.1")]
    [InlineData("1.1.1", "2.0.1")]
    [InlineData("2.0.0", "2.0.1")]
    [InlineData("3.0.2", "3.1.1")]
    [InlineData("3.2.2", "3.3.1")]
    [InlineData("3.3.3", "3.3.3")]
    public void VersionShouldNotBeNewer(string version, string previousVersion)
    {
        var result = _subject.IsVersionNewer(version, previousVersion);

        result.Should().BeFalse();
    }
}
