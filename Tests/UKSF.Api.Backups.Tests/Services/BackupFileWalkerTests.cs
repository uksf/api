using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupFileWalkerTests
{
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly BackupFileWalker _subject;

    public BackupFileWalkerTests()
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.GetFiles(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        _subject = new BackupFileWalker(_mockFileSystemProvider.Object);
    }

    private void GivenTree(string path, IEnumerable<string> directories, IEnumerable<string> files)
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(path)).Returns(directories.ToList());
        _mockFileSystemProvider.Setup(x => x.GetFiles(path)).Returns(files.ToList());
    }

    [Fact]
    public void A_folder_is_walked_recursively_and_named_under_files()
    {
        GivenTree(@"C:\Server\Nginx", [@"C:\Server\Nginx\conf"], [@"C:\Server\Nginx\nginx.exe"]);
        GivenTree(@"C:\Server\Nginx\conf", [], [@"C:\Server\Nginx\conf\nginx.conf"]);

        var result = _subject.Walk([new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder }]);

        result.Files.Select(x => x.EntryName).Should().BeEquivalentTo(["files/C/Server/Nginx/nginx.exe", "files/C/Server/Nginx/conf/nginx.conf"]);
        result.Skips.Should().BeEmpty();
    }

    [Fact]
    public void A_non_recursive_folder_takes_only_its_own_files()
    {
        GivenTree(@"C:\Server\Nginx", [@"C:\Server\Nginx\conf"], [@"C:\Server\Nginx\nginx.exe"]);
        GivenTree(@"C:\Server\Nginx\conf", [], [@"C:\Server\Nginx\conf\nginx.conf"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server\Nginx",
                    EntryType = BackupEntryType.Folder,
                    Recursive = false
                }
            ]
        );

        result.Files.Select(x => x.EntryName).Should().ContainSingle().Which.Should().Be("files/C/Server/Nginx/nginx.exe");
    }

    [Fact]
    public void An_excluded_folder_is_not_descended_into()
    {
        GivenTree(@"C:\Server\Nginx", [@"C:\Server\Nginx\logs", @"C:\Server\Nginx\conf"], []);
        GivenTree(@"C:\Server\Nginx\logs", [], [@"C:\Server\Nginx\logs\access.log"]);
        GivenTree(@"C:\Server\Nginx\conf", [], [@"C:\Server\Nginx\conf\nginx.conf"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server\Nginx",
                    EntryType = BackupEntryType.Folder,
                    Excludes = [@"C:\Server\Nginx\logs"]
                }
            ]
        );

        result.Files.Select(x => x.EntryName).Should().ContainSingle().Which.Should().Be("files/C/Server/Nginx/conf/nginx.conf");
    }

    [Fact]
    public void An_excluded_file_is_dropped_while_its_siblings_are_kept()
    {
        GivenTree(@"C:\Server\Teamspeak", [], [@"C:\Server\Teamspeak\deets.txt", @"C:\Server\Teamspeak\ts3server.log"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server\Teamspeak",
                    EntryType = BackupEntryType.Folder,
                    Excludes = [@"C:\Server\Teamspeak\ts3server.log"]
                }
            ]
        );

        result.Files.Select(x => x.EntryName).Should().ContainSingle().Which.Should().Be("files/C/Server/Teamspeak/deets.txt");
    }

    [Fact]
    public void An_exclude_nested_deeper_than_the_selection_still_applies()
    {
        GivenTree(@"C:\Server", [@"C:\Server\Nginx"], []);
        GivenTree(@"C:\Server\Nginx", [@"C:\Server\Nginx\logs"], []);
        GivenTree(@"C:\Server\Nginx\logs", [], [@"C:\Server\Nginx\logs\access.log"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server",
                    EntryType = BackupEntryType.Folder,
                    Excludes = [@"C:\Server\Nginx\logs"]
                }
            ]
        );

        result.Files.Should().BeEmpty();
    }

    [Fact]
    public void An_exclude_matches_a_path_the_provider_reports_differently_cased_or_slashed()
    {
        GivenTree(@"C:\Server\Nginx", [@"c:\SERVER\nginx\Logs\"], [@"C:\Server\Nginx\nginx.exe"]);
        GivenTree(@"c:\SERVER\nginx\Logs\", [], [@"c:\SERVER\nginx\Logs\access.log"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server\Nginx",
                    EntryType = BackupEntryType.Folder,
                    Excludes = [@"C:\Server\Nginx\logs"]
                }
            ]
        );

        result.Files.Select(x => x.EntryName).Should().ContainSingle().Which.Should().Be("files/C/Server/Nginx/nginx.exe");
    }

    [Fact]
    public void A_single_file_entry_is_taken_on_its_own()
    {
        var result = _subject.Walk([new DomainBackupEntry { Path = @"C:\Server\UKSF.Api\appsettings.json", EntryType = BackupEntryType.File }]);

        result.Files.Select(x => x.EntryName).Should().ContainSingle().Which.Should().Be("files/C/Server/UKSF.Api/appsettings.json");
    }

    [Fact]
    public void A_disabled_entry_is_ignored()
    {
        GivenTree(@"C:\Server\Nginx", [], [@"C:\Server\Nginx\nginx.exe"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server\Nginx",
                    EntryType = BackupEntryType.Folder,
                    Enabled = false
                }
            ]
        );

        result.Files.Should().BeEmpty();
    }

    [Fact]
    public void A_source_that_vanished_since_selection_is_skipped_not_thrown()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(@"C:\Server\Gone")).Returns(false);
        _mockFileSystemProvider.Setup(x => x.FileExists(@"C:\Server\gone.json")).Returns(false);

        var result = _subject.Walk(
            [
                new DomainBackupEntry { Path = @"C:\Server\Gone", EntryType = BackupEntryType.Folder },
                new DomainBackupEntry { Path = @"C:\Server\gone.json", EntryType = BackupEntryType.File }
            ]
        );

        result.Files.Should().BeEmpty();
        result.Skips.Should().HaveCount(2);
        result.Skips.Should().OnlyContain(x => x.Reason.Contains("no longer exists"));
    }

    [Fact]
    public void A_directory_that_denies_access_is_recorded_as_a_skip_and_the_walk_continues()
    {
        GivenTree(@"C:\Server", [@"C:\Server\Locked", @"C:\Server\Open"], []);
        _mockFileSystemProvider.Setup(x => x.GetFiles(@"C:\Server\Locked")).Throws(new UnauthorizedAccessException("Access to the path is denied"));
        GivenTree(@"C:\Server\Open", [], [@"C:\Server\Open\keep.txt"]);

        var result = _subject.Walk([new DomainBackupEntry { Path = @"C:\Server", EntryType = BackupEntryType.Folder }]);

        result.Files.Select(x => x.EntryName).Should().ContainSingle().Which.Should().Be("files/C/Server/Open/keep.txt");
        result.Skips.Should().ContainSingle().Which.Path.Should().Be(@"C:\Server\Locked");
    }

    [Fact]
    public void The_same_file_reached_twice_is_only_taken_once()
    {
        GivenTree(@"C:\Server\Nginx", [], [@"C:\Server\Nginx\nginx.exe"]);

        var result = _subject.Walk(
            [
                new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder },
                new DomainBackupEntry { Path = @"C:\Server\Nginx\nginx.exe", EntryType = BackupEntryType.File }
            ]
        );

        result.Files.Should().ContainSingle();
    }

    [Fact]
    public void A_locked_directory_listing_does_not_stop_files_already_found()
    {
        GivenTree(@"C:\Server", [], [@"C:\Server\keep.txt"]);
        _mockFileSystemProvider.Setup(x => x.GetDirectories(@"C:\Server")).Throws(new IOException("The device is not ready"));

        var result = _subject.Walk([new DomainBackupEntry { Path = @"C:\Server", EntryType = BackupEntryType.Folder }]);

        result.Files.Should().ContainSingle();
        result.Skips.Should().ContainSingle().Which.Reason.Should().Contain("device is not ready");
    }
}
