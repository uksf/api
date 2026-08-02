using FluentAssertions;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupPathsTests
{
    [Theory]
    [InlineData(@"C:/Server/Nginx", @"C:\Server\Nginx")]
    [InlineData(@"c:\server\nginx\", @"C:\server\nginx")]
    [InlineData(@"  D:\Website  ", @"D:\Website")]
    [InlineData(@"D:\\Modpack\\Certs", @"D:\Modpack\Certs")]
    [InlineData(@"E:\", @"E:\")]
    public void Normalise_produces_a_canonical_windows_path(string input, string expected)
    {
        BackupPaths.Normalise(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalise_rejects_an_empty_path(string input)
    {
        var act = () => BackupPaths.Normalise(input);

        act.Should().Throw<UksfException>().Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(@"C:\Server", @"C:\Server\Nginx", true)]
    [InlineData(@"C:\Server", @"c:\server\nginx\conf", true)]
    [InlineData(@"C:\Server", @"C:\Server", true)]
    [InlineData(@"C:\Server", @"C:\ServerOther", false)]
    [InlineData(@"C:\Server\Nginx", @"C:\Server", false)]
    [InlineData(@"C:\Server", @"D:\Server\Nginx", false)]
    public void Contains_only_matches_on_a_path_boundary(string parent, string child, bool expected)
    {
        BackupPaths.Contains(parent, child).Should().Be(expected);
    }
}
