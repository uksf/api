using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupSelectionServiceTests
{
    private readonly Mock<IBackupEntriesContext> _mockContext = new();
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly BackupSelectionService _subject;
    private List<DomainBackupEntry> _entries = [];

    public BackupSelectionServiceTests()
    {
        _mockContext.Setup(x => x.Get()).Returns(() => _entries);
        _mockContext.Setup(x => x.Get(It.IsAny<Func<DomainBackupEntry, bool>>()))
                    .Returns((Func<DomainBackupEntry, bool> predicate) => _entries.Where(predicate));
        _mockContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns((string id) => _entries.FirstOrDefault(x => x.Id == id));
        _mockContext.Setup(x => x.Add(It.IsAny<DomainBackupEntry>())).Callback((DomainBackupEntry entry) => _entries.Add(entry)).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainBackupEntry>()))
                    .Callback((DomainBackupEntry entry) =>
                        {
                            _entries.RemoveAll(x => x.Id == entry.Id);
                            _entries.Add(entry);
                        }
                    )
                    .Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Delete(It.IsAny<string>())).Callback((string id) => _entries.RemoveAll(x => x.Id == id)).Returns(Task.CompletedTask);

        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        _subject = new BackupSelectionService(_mockContext.Object, _mockFileSystemProvider.Object);
    }

    [Fact]
    public async Task Adding_a_folder_normalises_the_path_and_defaults_to_recursive_and_enabled()
    {
        var result = await _subject.AddEntry(new DomainBackupEntry { Path = @"C:/Server/Nginx/", EntryType = BackupEntryType.Folder });

        result.Path.Should().Be(@"C:\Server\Nginx");
        result.Recursive.Should().BeTrue();
        result.Enabled.Should().BeTrue();
        _entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Adding_a_folder_that_does_not_exist_is_rejected()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

        var act = () => _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Nope", EntryType = BackupEntryType.Folder });

        (await act.Should().ThrowAsync<UksfException>()).Which.StatusCode.Should().Be(400);
        _entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Adding_a_file_that_does_not_exist_is_rejected()
    {
        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        var act = () => _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\gone.json", EntryType = BackupEntryType.File });

        (await act.Should().ThrowAsync<UksfException>()).Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Adding_the_same_path_twice_is_rejected()
    {
        await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder });

        var act = () => _subject.AddEntry(new DomainBackupEntry { Path = @"c:\server\nginx", EntryType = BackupEntryType.Folder });

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("already selected");
    }

    [Fact]
    public async Task Adding_a_path_inside_an_existing_recursive_folder_is_rejected()
    {
        await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server", EntryType = BackupEntryType.Folder });

        var act = () => _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx\conf", EntryType = BackupEntryType.Folder });

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("overlaps");
    }

    [Fact]
    public async Task Adding_a_folder_that_would_swallow_an_existing_entry_is_rejected()
    {
        await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx\conf", EntryType = BackupEntryType.Folder });

        var act = () => _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server", EntryType = BackupEntryType.Folder });

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("overlaps");
    }

    [Fact]
    public async Task A_non_recursive_folder_does_not_block_a_deeper_selection()
    {
        await _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server",
                EntryType = BackupEntryType.Folder,
                Recursive = false
            }
        );

        var result = await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder });

        result.Path.Should().Be(@"C:\Server\Nginx");
        _entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Excludes_are_normalised_and_deduplicated()
    {
        var result = await _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Nginx",
                EntryType = BackupEntryType.Folder,
                Excludes = [@"C:/Server/Nginx/logs", @"c:\server\nginx\logs\", @"C:\Server\Nginx\temp"]
            }
        );

        result.Excludes.Should().BeEquivalentTo([@"C:\Server\Nginx\logs", @"C:\Server\Nginx\temp"]);
    }

    [Fact]
    public async Task An_exclude_outside_the_selected_folder_is_rejected()
    {
        var act = () => _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Nginx",
                EntryType = BackupEntryType.Folder,
                Excludes = [@"C:\Server\Teamspeak"]
            }
        );

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("not inside");
    }

    [Fact]
    public async Task An_exclude_equal_to_the_selected_folder_is_rejected()
    {
        var act = () => _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Nginx",
                EntryType = BackupEntryType.Folder,
                Excludes = [@"C:\Server\Nginx"]
            }
        );

        (await act.Should().ThrowAsync<UksfException>()).Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Patterns_are_trimmed_and_deduplicated()
    {
        var result = await _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Arma\Profiles",
                EntryType = BackupEntryType.Folder,
                IncludePatterns = [" *.Arma3Profile ", "*.ARMA3PROFILE", "*.vars.*", "  "]
            }
        );

        result.IncludePatterns.Should().BeEquivalentTo(["*.Arma3Profile", "*.vars.*"]);
    }

    [Fact]
    public async Task A_pattern_containing_a_path_is_rejected()
    {
        var act = () => _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Arma\Profiles",
                EntryType = BackupEntryType.Folder,
                IncludePatterns = [@"Users\*.Arma3Profile"]
            }
        );

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("cannot contain a path");
    }

    [Fact]
    public async Task Patterns_on_a_file_entry_are_rejected()
    {
        var act = () => _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Teamspeak\deets.txt",
                EntryType = BackupEntryType.File,
                IncludePatterns = ["*.txt"]
            }
        );

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("folder entry");
    }

    [Fact]
    public async Task A_name_pattern_exclude_is_kept_as_typed_and_not_treated_as_a_path()
    {
        var result = await _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\Arma\Profiles",
                EntryType = BackupEntryType.Folder,
                Excludes = ["DevRun_*"]
            }
        );

        result.Excludes.Should().ContainSingle().Which.Should().Be("DevRun_*");
    }

    [Fact]
    public async Task Excludes_on_a_file_entry_are_rejected()
    {
        var act = () => _subject.AddEntry(
            new DomainBackupEntry
            {
                Path = @"C:\Server\UKSF.Api\appsettings.json",
                EntryType = BackupEntryType.File,
                Excludes = [@"C:\Server\UKSF.Api\appsettings.json"]
            }
        );

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("folder entry");
    }

    [Fact]
    public async Task Updating_an_entry_replaces_it_without_tripping_the_overlap_check_against_itself()
    {
        var added = await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder });

        added.Excludes = [@"C:\Server\Nginx\logs"];
        var result = await _subject.UpdateEntry(added);

        result.Excludes.Should().ContainSingle().Which.Should().Be(@"C:\Server\Nginx\logs");
        _entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Updating_an_unknown_entry_is_rejected()
    {
        var act = () => _subject.UpdateEntry(
            new DomainBackupEntry
            {
                Id = "5bd9daa3b1c98150403bccf6",
                Path = @"C:\Server",
                EntryType = BackupEntryType.Folder
            }
        );

        (await act.Should().ThrowAsync<UksfException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Deleting_an_entry_removes_it()
    {
        var added = await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder });

        await _subject.DeleteEntry(added.Id);

        _entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_an_unknown_entry_is_rejected()
    {
        var act = () => _subject.DeleteEntry("5bd9daa3b1c98150403bccf6");

        (await act.Should().ThrowAsync<UksfException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Entries_come_back_sorted_by_path()
    {
        await _subject.AddEntry(new DomainBackupEntry { Path = @"D:\Website", EntryType = BackupEntryType.Folder });
        await _subject.AddEntry(new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder });

        _subject.GetEntries().Select(x => x.Path).Should().ContainInOrder(@"C:\Server\Nginx", @"D:\Website");
    }
}
