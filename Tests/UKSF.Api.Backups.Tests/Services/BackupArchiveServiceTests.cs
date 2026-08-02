using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupArchiveServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBackupFileWalker> _mockBackupFileWalker = new();
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly BackupArchiveService _subject;

    public BackupArchiveServiceTests()
    {
        var mockClock = new Mock<IClock>();
        mockClock.Setup(x => x.UtcNow()).Returns(Now);

        _mockFileSystemProvider.Setup(x => x.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(Now);

        _subject = new BackupArchiveService(_mockFileSystemProvider.Object, _mockBackupFileWalker.Object, mockClock.Object, _mockLogger.Object);
    }

    private void GivenWalk(BackupWalkResult walk)
    {
        _mockBackupFileWalker.Setup(x => x.Walk(It.IsAny<IReadOnlyList<DomainBackupEntry>>())).Returns(walk);
    }

    private void GivenFile(string path, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        _mockFileSystemProvider.Setup(x => x.GetFileSize(path)).Returns(bytes.Length);
        _mockFileSystemProvider.Setup(x => x.OpenRead(path)).Returns(() => new MemoryStream(bytes));
    }

    private static Dictionary<string, string> ReadArchive(Stream stream)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(
            x => x.FullName,
            x =>
            {
                using var reader = new StreamReader(x.Open());
                return reader.ReadToEnd();
            }
        );
    }

    [Fact]
    public async Task Files_are_written_at_their_entry_names_with_a_manifest()
    {
        GivenWalk(
            new BackupWalkResult
            {
                Files =
                [
                    new BackupWalkFile
                    {
                        SourcePath = @"C:\Server\Nginx\conf\nginx.conf",
                        EntryName = "files/C/Server/Nginx/conf/nginx.conf",
                        SelectionPath = @"C:\Server\Nginx"
                    }
                ]
            }
        );
        GivenFile(@"C:\Server\Nginx\conf\nginx.conf", "worker_processes 1;");

        using var output = new MemoryStream();
        var manifest = await _subject.WriteArchive([new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder }], output);

        var contents = ReadArchive(output);
        contents.Should().ContainKey("files/C/Server/Nginx/conf/nginx.conf").WhoseValue.Should().Be("worker_processes 1;");
        contents.Should().ContainKey("manifest.json");
        manifest.FileCount.Should().Be(1);
        manifest.RawBytes.Should().Be(19);
        manifest.CreatedUtc.Should().Be(Now);
    }

    [Fact]
    public async Task The_manifest_records_the_selection_and_per_entry_totals()
    {
        GivenWalk(
            new BackupWalkResult
            {
                Files =
                [
                    new BackupWalkFile
                    {
                        SourcePath = @"C:\a.txt",
                        EntryName = "files/C/a.txt",
                        SelectionPath = @"C:\Server"
                    },
                    new BackupWalkFile
                    {
                        SourcePath = @"C:\b.txt",
                        EntryName = "files/C/b.txt",
                        SelectionPath = @"C:\Server"
                    },
                    new BackupWalkFile
                    {
                        SourcePath = @"D:\c.txt",
                        EntryName = "files/D/c.txt",
                        SelectionPath = @"D:\Website"
                    }
                ]
            }
        );
        GivenFile(@"C:\a.txt", "aa");
        GivenFile(@"C:\b.txt", "bbb");
        GivenFile(@"D:\c.txt", "cccc");

        using var output = new MemoryStream();
        var manifest = await _subject.WriteArchive(
            [
                new DomainBackupEntry
                {
                    Path = @"C:\Server",
                    EntryType = BackupEntryType.Folder,
                    Excludes = [@"C:\Server\Nginx\logs"]
                },
                new DomainBackupEntry { Path = @"D:\Website", EntryType = BackupEntryType.Folder }
            ],
            output
        );

        var serialised = JsonSerializer.Deserialize<BackupManifest>(ReadArchive(output)["manifest.json"]);
        serialised.FileCount.Should().Be(3);
        serialised.RawBytes.Should().Be(9);
        serialised.Entries.Should().HaveCount(2);
        serialised.Entries.Single(x => x.Path == @"C:\Server").FileCount.Should().Be(2);
        serialised.Entries.Single(x => x.Path == @"C:\Server").RawBytes.Should().Be(5);
        serialised.Entries.Single(x => x.Path == @"C:\Server").Excludes.Should().ContainSingle();
        serialised.Entries.Single(x => x.Path == @"D:\Website").FileCount.Should().Be(1);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_skipped_and_the_rest_still_archive()
    {
        GivenWalk(
            new BackupWalkResult
            {
                Files =
                [
                    new BackupWalkFile
                    {
                        SourcePath = @"C:\locked.db",
                        EntryName = "files/C/locked.db",
                        SelectionPath = @"C:\Server"
                    },
                    new BackupWalkFile
                    {
                        SourcePath = @"C:\keep.txt",
                        EntryName = "files/C/keep.txt",
                        SelectionPath = @"C:\Server"
                    }
                ]
            }
        );
        _mockFileSystemProvider.Setup(x => x.GetFileSize(@"C:\locked.db")).Returns(10);
        _mockFileSystemProvider.Setup(x => x.OpenRead(@"C:\locked.db")).Throws(new IOException("The process cannot access the file"));
        GivenFile(@"C:\keep.txt", "keep");

        using var output = new MemoryStream();
        var manifest = await _subject.WriteArchive([new DomainBackupEntry { Path = @"C:\Server", EntryType = BackupEntryType.Folder }], output);

        manifest.FileCount.Should().Be(1);
        manifest.Skips.Should().ContainSingle().Which.Path.Should().Be(@"C:\locked.db");
        ReadArchive(output).Keys.Should().Contain("files/C/keep.txt");
        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(y => y.Contains(@"C:\locked.db"))), Times.Once);
    }

    [Fact]
    public async Task Walk_skips_are_carried_into_the_manifest()
    {
        GivenWalk(new BackupWalkResult { Skips = [new BackupSkip { Path = @"C:\Server\Gone", Reason = "Folder no longer exists" }] });

        using var output = new MemoryStream();
        var manifest = await _subject.WriteArchive([], output);

        manifest.Skips.Should().ContainSingle().Which.Reason.Should().Be("Folder no longer exists");
        manifest.FileCount.Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_stops_the_archive()
    {
        GivenWalk(
            new BackupWalkResult
            {
                Files =
                [
                    new BackupWalkFile
                    {
                        SourcePath = @"C:\a.txt",
                        EntryName = "files/C/a.txt",
                        SelectionPath = @"C:\Server"
                    }
                ]
            }
        );
        GivenFile(@"C:\a.txt", "aa");

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        using var output = new MemoryStream();
        var act = () => _subject.WriteArchive([], output, null, cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _mockFileSystemProvider.Verify(x => x.OpenRead(It.IsAny<string>()), Times.Never);
    }
}
