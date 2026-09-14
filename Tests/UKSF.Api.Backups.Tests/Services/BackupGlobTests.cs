using FluentAssertions;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupGlobTests
{
    [Theory]
    [InlineData("*.Arma3Profile", @"C:\Server\Arma\Profiles\Main\Users\Main\Main.Arma3Profile", true)]
    [InlineData("*.vars.*", @"C:\Server\Arma\Profiles\Main\Users\Main\Main.vars.Arma3Profile", true)]
    [InlineData("*.vars.*", @"C:\Server\Arma\Profiles\Main\Users\Main\Main.Arma3Profile", false)]
    [InlineData("*.Arma3Profile", @"C:\Server\Arma\Profiles\Main\Users\Main\Main.vars.Arma3Profile", true)]
    [InlineData("*.Arma3Profile", @"C:\Server\Arma\Profiles\Main\mpStatistics_9240.log", false)]
    [InlineData("DevRun_*", @"C:\Server\Arma\Profiles\DevRun_173767a6", true)]
    [InlineData("DevRun_*", @"C:\Server\Arma\Profiles\Main", false)]
    [InlineData("*.LOG", @"C:\Server\Nginx\logs\access.log", true)]
    [InlineData("access.?og", @"C:\Server\Nginx\logs\access.log", true)]
    public void A_pattern_matches_the_last_segment_only(string pattern, string path, bool expected)
    {
        BackupGlob.MatchesName(pattern, path).Should().Be(expected);
    }

    [Fact]
    public void A_trailing_separator_does_not_hide_the_name()
    {
        BackupGlob.MatchesName("DevRun_*", @"C:\Server\Arma\Profiles\DevRun_2b13eb76\").Should().BeTrue();
    }

    [Fact]
    public void A_pattern_never_matches_a_parent_folder_name_by_accident()
    {
        BackupGlob.MatchesName("Profiles", @"C:\Server\Arma\Profiles\Main\Main.Arma3Profile").Should().BeFalse();
    }

    [Theory]
    [InlineData("*.log", true)]
    [InlineData("DevRun_?", true)]
    [InlineData("OLD USERS BACKUP", false)]
    [InlineData(null, false)]
    public void A_glob_is_recognised_by_its_wildcards(string pattern, bool expected)
    {
        BackupGlob.IsGlob(pattern).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Server\Nginx\logs", true)]
    [InlineData("C:/Server/Nginx/logs", true)]
    [InlineData("*.log", false)]
    public void A_separator_marks_a_path_rather_than_a_name(string pattern, bool expected)
    {
        BackupGlob.HasSeparator(pattern).Should().Be(expected);
    }

    [Fact]
    public void Nothing_matches_an_empty_pattern_or_path()
    {
        BackupGlob.MatchesName("   ", @"C:\file.txt").Should().BeFalse();
        BackupGlob.MatchesName("*.txt", "  ").Should().BeFalse();
    }
}
