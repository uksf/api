using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupRetentionServiceTests
{
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly Mock<IGoogleDriveClient> _mockGoogleDriveClient = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly BackupRetentionService _subject;

    public BackupRetentionServiceTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable(It.IsAny<string>())).Returns((string key) => new DomainVariableItem { Key = key });
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockGoogleDriveClient.Setup(x => x.List(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _subject = new BackupRetentionService(_mockVariablesService.Object, _mockGoogleDriveClient.Object, _mockFileSystemProvider.Object, _mockLogger.Object);
    }

    private static BackupCloudFile Cloud(string name, long bytes = 100)
    {
        return new BackupCloudFile
        {
            Id = $"id-{name}",
            Name = name,
            Bytes = bytes
        };
    }

    private void GivenRemote(params BackupCloudFile[] files)
    {
        _mockGoogleDriveClient.Setup(x => x.List(It.IsAny<CancellationToken>())).ReturnsAsync(files.ToList());
    }

    private void GivenQuota(long limit, long used)
    {
        _mockGoogleDriveClient.Setup(x => x.GetQuota(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new BackupCloudQuota { LimitBytes = limit, UsedBytes = used });
    }

    [Fact]
    public void Retention_defaults_to_two_and_never_drops_below_one()
    {
        _subject.Retention.Should().Be(2);

        _mockVariablesService.Setup(x => x.GetVariable("BACKUP_RETENTION")).Returns(new DomainVariableItem { Key = "BACKUP_RETENTION", Item = "0" });
        _subject.Retention.Should().Be(1);

        _mockVariablesService.Setup(x => x.GetVariable("BACKUP_RETENTION")).Returns(new DomainVariableItem { Key = "BACKUP_RETENTION", Item = "5" });
        _subject.Retention.Should().Be(5);
    }

    [Fact]
    public void Local_prune_keeps_the_newest_by_stamp_and_deletes_the_rest()
    {
        _mockFileSystemProvider.Setup(x => x.GetFiles(@"E:\Backups"))
                               .Returns(
                                   [
                                       @"E:\Backups\uksf-backup-20260731-040000.zip.enc",
                                       @"E:\Backups\uksf-backup-20260802-040000.zip.enc",
                                       @"E:\Backups\uksf-backup-20260801-040000.zip.enc",
                                       @"E:\Backups\uksf-backup-20260730-040000.zip.enc"
                                   ]
                               );

        _subject.PruneLocal(@"E:\Backups");

        _mockFileSystemProvider.Verify(x => x.DeleteFile(@"E:\Backups\uksf-backup-20260731-040000.zip.enc"), Times.Once);
        _mockFileSystemProvider.Verify(x => x.DeleteFile(@"E:\Backups\uksf-backup-20260730-040000.zip.enc"), Times.Once);
        _mockFileSystemProvider.Verify(x => x.DeleteFile(@"E:\Backups\uksf-backup-20260802-040000.zip.enc"), Times.Never);
        _mockFileSystemProvider.Verify(x => x.DeleteFile(@"E:\Backups\uksf-backup-20260801-040000.zip.enc"), Times.Never);
    }

    [Fact]
    public void Local_prune_ignores_files_that_are_not_backup_archives()
    {
        _mockFileSystemProvider.Setup(x => x.GetFiles(@"E:\Backups"))
                               .Returns([@"E:\Backups\notes.txt", @"E:\Backups\uksf-backup-20260802-040000.zip.enc", @"E:\Backups\old.zip"]);

        _subject.PruneLocal(@"E:\Backups");

        _mockFileSystemProvider.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Local_prune_on_a_missing_directory_does_nothing()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

        _subject.PruneLocal(@"E:\Backups");

        _mockFileSystemProvider.Verify(x => x.GetFiles(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Remote_prune_keeps_the_newest_two()
    {
        GivenRemote(Cloud("uksf-backup-20260730-040000.zip.enc"), Cloud("uksf-backup-20260802-040000.zip.enc"), Cloud("uksf-backup-20260801-040000.zip.enc"));

        await _subject.PruneRemote();

        _mockGoogleDriveClient.Verify(x => x.Delete("id-uksf-backup-20260730-040000.zip.enc", It.IsAny<CancellationToken>()), Times.Once);
        _mockGoogleDriveClient.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Remote_prune_leaves_other_files_in_the_folder_alone()
    {
        GivenRemote(Cloud("keys.txt"), Cloud("uksf-backup-20260802-040000.zip.enc"), Cloud("holiday-photo.jpg"));

        await _subject.PruneRemote();

        _mockGoogleDriveClient.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Space_is_not_touched_when_the_quota_already_fits_the_upload()
    {
        GivenQuota(15_000, 1_000);

        await _subject.EnsureRemoteSpace(5_000);

        _mockGoogleDriveClient.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_oldest_archive_is_deleted_first_to_make_room()
    {
        GivenQuota(15_000, 14_000);
        GivenRemote(
            Cloud("uksf-backup-20260802-040000.zip.enc", 3_000),
            Cloud("uksf-backup-20260801-040000.zip.enc", 3_000),
            Cloud("uksf-backup-20260731-040000.zip.enc", 3_000)
        );

        await _subject.EnsureRemoteSpace(2_500);

        _mockGoogleDriveClient.Verify(x => x.Delete("id-uksf-backup-20260731-040000.zip.enc", It.IsAny<CancellationToken>()), Times.Once);
        _mockGoogleDriveClient.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task More_archives_are_deleted_until_the_upload_fits()
    {
        GivenQuota(15_000, 14_500);
        GivenRemote(
            Cloud("uksf-backup-20260802-040000.zip.enc", 3_000),
            Cloud("uksf-backup-20260801-040000.zip.enc", 3_000),
            Cloud("uksf-backup-20260731-040000.zip.enc", 3_000)
        );

        await _subject.EnsureRemoteSpace(6_000);

        _mockGoogleDriveClient.Verify(x => x.Delete("id-uksf-backup-20260731-040000.zip.enc", It.IsAny<CancellationToken>()), Times.Once);
        _mockGoogleDriveClient.Verify(x => x.Delete("id-uksf-backup-20260801-040000.zip.enc", It.IsAny<CancellationToken>()), Times.Once);
        _mockGoogleDriveClient.Verify(x => x.Delete("id-uksf-backup-20260802-040000.zip.enc", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_newest_archive_is_never_deleted_to_make_room()
    {
        GivenQuota(15_000, 14_900);
        GivenRemote(Cloud("uksf-backup-20260802-040000.zip.enc", 5_000));

        var act = () => _subject.EnsureRemoteSpace(4_000);

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("every prunable archive is already gone");
        _mockGoogleDriveClient.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Running_out_of_room_after_pruning_fails_loudly()
    {
        GivenQuota(15_000, 14_900);
        GivenRemote(Cloud("uksf-backup-20260802-040000.zip.enc", 100), Cloud("uksf-backup-20260801-040000.zip.enc", 100));

        var act = () => _subject.EnsureRemoteSpace(10_000);

        (await act.Should().ThrowAsync<UksfException>()).Which.StatusCode.Should().Be(500);
        _mockGoogleDriveClient.Verify(x => x.Delete("id-uksf-backup-20260801-040000.zip.enc", It.IsAny<CancellationToken>()), Times.Once);
    }
}
