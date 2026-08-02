using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupRunnerTests
{
    private readonly BackupRunnerHarness _harness = new();

    [Fact]
    public async Task A_successful_run_dumps_archives_encrypts_uploads_and_prunes_in_order()
    {
        var sequence = new List<string>();
        _harness.MockMongoDumpService.Setup(x => x.Dump(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("dump"))
                .ReturnsAsync([new MongoDumpFile { Database = "all", Path = @"E:\Backups\staging\mongo\all.archive.gz" }]);
        _harness.MockArchiveService.Setup(x => x.WriteArchive(
                                              It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                                              It.IsAny<Stream>(),
                                              It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                                              It.IsAny<CancellationToken>()
                                          )
                )
                .Callback(() => sequence.Add("archive"))
                .ReturnsAsync(new BackupManifest());
        _harness.MockEncryptionService.Setup(x => x.Encrypt(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("encrypt"))
                .Returns(Task.CompletedTask);
        _harness.MockRetentionService.Setup(x => x.PruneLocal(It.IsAny<string>())).Callback(() => sequence.Add("prune-local"));
        _harness.MockRetentionService.Setup(x => x.EnsureRemoteSpace(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("ensure-space"))
                .Returns(Task.CompletedTask);
        _harness.MockGoogleDriveClient.Setup(x => x.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("upload"))
                .ReturnsAsync(new BackupCloudFile { Id = "drive-id" });
        _harness.MockRetentionService.Setup(x => x.PruneRemote(It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("prune-remote"))
                .Returns(Task.CompletedTask);

        var run = await _harness.Subject.Run();

        sequence.Should().ContainInOrder("dump", "archive", "encrypt", "prune-local", "ensure-space", "upload", "prune-remote");
        run.State.Should().Be(BackupRunState.Success);
        run.DriveFileId.Should().Be("drive-id");
    }

    [Fact]
    public async Task Start_returns_the_running_record_without_waiting_for_the_backup()
    {
        var archiveStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _harness.MockArchiveService.Setup(x => x.WriteArchive(
                                              It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                                              It.IsAny<Stream>(),
                                              It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                                              It.IsAny<CancellationToken>()
                                          )
                )
                .Returns(async () =>
                    {
                        archiveStarted.TrySetResult();
                        await release.Task;
                        return new BackupManifest();
                    }
                );

        var run = await _harness.Subject.Start();

        run.State.Should().Be(BackupRunState.Running);
        run.Finished.Should().BeNull();

        await archiveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
    }

    [Fact]
    public async Task The_run_is_recorded_before_any_work_so_a_crash_still_leaves_a_trace()
    {
        await _harness.Subject.Run();

        _harness.Runs.Should().ContainSingle();
        _harness.StateWhenAdded.Should().Be(BackupRunState.Running);
    }

    [Fact]
    public async Task A_successful_run_records_counts_name_and_skips()
    {
        var run = await _harness.Subject.Run();

        run.ArchiveName.Should().Be("uksf-backup-20260802-030000.zip.enc");
        run.LocalPath.Should().Be(@"E:\Backups\uksf-backup-20260802-030000.zip.enc");
        run.FileCount.Should().Be(12);
        run.RawBytes.Should().Be(2048);
        run.ArchiveBytes.Should().Be(1024);
        run.Databases.Should().ContainSingle().Which.Should().Be("all");
        run.Skips.Should().ContainSingle();
        run.Finished.Should().Be(BackupRunnerHarness.Now);
    }

    [Fact]
    public async Task Mongo_dumps_are_added_to_the_archive_under_mongo()
    {
        IReadOnlyList<BackupWalkFile> extras = null;
        _harness.MockArchiveService
                .Setup(x => x.WriteArchive(
                           It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                           It.IsAny<Stream>(),
                           It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                           It.IsAny<CancellationToken>()
                       )
                )
                .Callback((IReadOnlyList<DomainBackupEntry> _, Stream _, IReadOnlyList<BackupWalkFile> files, CancellationToken _) => extras = files)
                .ReturnsAsync(new BackupManifest());

        await _harness.Subject.Run();

        extras.Should().ContainSingle();
        extras[0].EntryName.Should().Be("mongo/all.archive.gz");
    }

    [Fact]
    public async Task Disabled_entries_are_not_archived()
    {
        _harness.MockSelectionService.Setup(x => x.GetEntries())
                .Returns(
                    [
                        new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder },
                        new DomainBackupEntry
                        {
                            Path = @"C:\Server\Old",
                            EntryType = BackupEntryType.Folder,
                            Enabled = false
                        }
                    ]
                );
        IReadOnlyList<DomainBackupEntry> archived = null;
        _harness.MockArchiveService
                .Setup(x => x.WriteArchive(
                           It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                           It.IsAny<Stream>(),
                           It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                           It.IsAny<CancellationToken>()
                       )
                )
                .Callback((IReadOnlyList<DomainBackupEntry> entries, Stream _, IReadOnlyList<BackupWalkFile> _, CancellationToken _) => archived = entries)
                .ReturnsAsync(new BackupManifest());

        await _harness.Subject.Run();

        archived.Should().ContainSingle().Which.Path.Should().Be(@"C:\Server\Nginx");
    }

    [Fact]
    public async Task Upload_is_skipped_when_the_drive_leg_is_turned_off()
    {
        _harness.GivenVariable("BACKUP_DRIVE_ENABLED", "false");

        var run = await _harness.Subject.Run();

        run.State.Should().Be(BackupRunState.Success);
        run.DriveFileId.Should().BeNull();
        _harness.MockGoogleDriveClient.Verify(x => x.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_backup_path_can_be_moved_with_a_variable()
    {
        _harness.GivenVariable("BACKUP_PATH", @"D:\OtherBackups");

        var run = await _harness.Subject.Run();

        run.LocalPath.Should().StartWith(@"D:\OtherBackups\");
        _harness.MockRetentionService.Verify(x => x.PruneLocal(@"D:\OtherBackups"), Times.Once);
    }
}
