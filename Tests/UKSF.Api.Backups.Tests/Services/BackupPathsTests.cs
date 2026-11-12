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
        BackupPaths.Normalise(input, true).Should().Be(expected);
    }

    [Theory]
    [InlineData("/srv/nginx", "/srv/nginx")]
    [InlineData("/srv/nginx/", "/srv/nginx")]
    [InlineData("  /srv//Nginx  ", "/srv/Nginx")]
    [InlineData(@"\srv\nginx", "/srv/nginx")]
    [InlineData("/", "/")]
    public void Normalise_produces_a_canonical_unix_path(string input, string expected)
    {
        BackupPaths.Normalise(input, false).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Server", true)]
    [InlineData("/srv", false)]
    public void Normalise_rejects_a_path_from_the_other_platform(string input, bool fromWindows)
    {
        var act = () => BackupPaths.Normalise(input, !fromWindows);

        act.Should().Throw<UksfException>().Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData("srv/nginx", false)]
    [InlineData(@"Server\Nginx", true)]
    public void Normalise_rejects_a_relative_path(string input, bool windows)
    {
        var act = () => BackupPaths.Normalise(input, windows);

        act.Should().Throw<UksfException>().Which.StatusCode.Should().Be(400);
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
    public void Contains_only_matches_on_a_windows_path_boundary(string parent, string child, bool expected)
    {
        BackupPaths.Contains(parent, child, true).Should().Be(expected);
    }

    [Theory]
    [InlineData("/srv", "/srv/nginx", true)]
    [InlineData("/srv", "/srv", true)]
    [InlineData("/", "/srv/nginx", true)]
    [InlineData("/srv", "/srvother", false)]
    [InlineData("/srv", "/SRV/nginx", false)]
    [InlineData("/srv/nginx", "/srv", false)]
    public void Contains_is_case_sensitive_on_a_unix_path_boundary(string parent, string child, bool expected)
    {
        BackupPaths.Contains(parent, child, false).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Server\Nginx\nginx.conf", true, "files/C/Server/Nginx/nginx.conf")]
    [InlineData("/srv/nginx/nginx.conf", false, "files/srv/nginx/nginx.conf")]
    public void ToEntryName_maps_a_path_to_a_zip_entry(string path, bool windows, string expected)
    {
        BackupPaths.ToEntryName(path, windows).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Server\Nginx", true, "Nginx")]
    [InlineData(@"E:\", true, @"E:\")]
    [InlineData("/srv/nginx", false, "nginx")]
    [InlineData("/", false, "/")]
    public void LastSegment_names_the_final_folder_or_the_root(string path, bool windows, string expected)
    {
        BackupPaths.LastSegment(path, windows).Should().Be(expected);
    }
}
