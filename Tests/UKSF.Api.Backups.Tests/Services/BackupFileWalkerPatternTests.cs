using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

/// <summary>The Arma profiles case: folders appear and disappear with game server entries, only two file kinds matter.</summary>
public class BackupFileWalkerPatternTests
{
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly BackupFileWalker _subject;

    public BackupFileWalkerPatternTests()
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.GetFiles(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        GivenTree(
            @"C:\Server\Arma\Profiles",
            [@"C:\Server\Arma\Profiles\Main", @"C:\Server\Arma\Profiles\DevRun_173767a6", @"C:\Server\Arma\Profiles\OLD USERS BACKUP"],
            []
        );
        GivenTree(@"C:\Server\Arma\Profiles\Main", [@"C:\Server\Arma\Profiles\Main\Users"], [@"C:\Server\Arma\Profiles\Main\mpStatistics_9240.log"]);
        GivenTree(@"C:\Server\Arma\Profiles\Main\Users", [@"C:\Server\Arma\Profiles\Main\Users\Main"], []);
        GivenTree(
            @"C:\Server\Arma\Profiles\Main\Users\Main",
            [],
            [
                @"C:\Server\Arma\Profiles\Main\Users\Main\Main.Arma3Profile",
                @"C:\Server\Arma\Profiles\Main\Users\Main\Main.vars.Arma3Profile",
                @"C:\Server\Arma\Profiles\Main\Users\Main\arma3server_x64_2026-07-30.rpt"
            ]
        );
        GivenTree(@"C:\Server\Arma\Profiles\DevRun_173767a6", [], [@"C:\Server\Arma\Profiles\DevRun_173767a6\uksfe.Arma3Profile"]);
        GivenTree(@"C:\Server\Arma\Profiles\OLD USERS BACKUP", [], [@"C:\Server\Arma\Profiles\OLD USERS BACKUP\Dev.Arma3Profile"]);

        _subject = new BackupFileWalker(_mockFileSystemProvider.Object);
    }

    private void GivenTree(string path, IEnumerable<string> directories, IEnumerable<string> files)
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(path)).Returns(directories.ToList());
        _mockFileSystemProvider.Setup(x => x.GetFiles(path)).Returns(files.ToList());
    }

    private static DomainBackupEntry Profiles(List<string> patterns, List<string> excludes = null)
    {
        return new DomainBackupEntry
        {
            Path = @"C:\Server\Arma\Profiles",
            EntryType = BackupEntryType.Folder,
            IncludePatterns = patterns,
            Excludes = excludes ?? []
        };
    }

    [Fact]
    public void Only_files_matching_a_pattern_are_taken()
    {
        var result = _subject.Walk([Profiles(["*.Arma3Profile", "*.vars.*"])]);

        result.Files.Select(x => x.SourcePath)
              .Should()
              .BeEquivalentTo(
                  [
                      @"C:\Server\Arma\Profiles\Main\Users\Main\Main.Arma3Profile",
                      @"C:\Server\Arma\Profiles\Main\Users\Main\Main.vars.Arma3Profile",
                      @"C:\Server\Arma\Profiles\DevRun_173767a6\uksfe.Arma3Profile",
                      @"C:\Server\Arma\Profiles\OLD USERS BACKUP\Dev.Arma3Profile"
                  ]
              );
    }

    [Fact]
    public void A_profile_folder_added_later_is_picked_up_without_touching_the_selection()
    {
        GivenTree(
            @"C:\Server\Arma\Profiles",
            [
                @"C:\Server\Arma\Profiles\Main", @"C:\Server\Arma\Profiles\DevRun_173767a6", @"C:\Server\Arma\Profiles\OLD USERS BACKUP",
                @"C:\Server\Arma\Profiles\Quinary"
            ],
            []
        );
        GivenTree(@"C:\Server\Arma\Profiles\Quinary", [], [@"C:\Server\Arma\Profiles\Quinary\Quinary.vars.Arma3Profile"]);

        var result = _subject.Walk([Profiles(["*.Arma3Profile", "*.vars.*"])]);

        result.Files.Select(x => x.SourcePath).Should().Contain(@"C:\Server\Arma\Profiles\Quinary\Quinary.vars.Arma3Profile");
    }

    [Fact]
    public void A_name_pattern_exclude_prunes_folders_at_any_depth()
    {
        var result = _subject.Walk([Profiles(["*.Arma3Profile", "*.vars.*"], ["DevRun_*"])]);

        result.Files.Select(x => x.SourcePath).Should().NotContain(x => x.Contains("DevRun_"));
        result.Files.Should().HaveCount(3);
    }

    [Fact]
    public void A_path_exclude_still_works_alongside_patterns()
    {
        var result = _subject.Walk([Profiles(["*.Arma3Profile"], [@"C:\Server\Arma\Profiles\OLD USERS BACKUP"])]);

        result.Files.Select(x => x.SourcePath).Should().NotContain(x => x.Contains("OLD USERS"));
    }

    [Fact]
    public void No_patterns_means_everything_as_before()
    {
        var result = _subject.Walk([Profiles([])]);

        result.Files.Should().HaveCount(6);
    }
}
