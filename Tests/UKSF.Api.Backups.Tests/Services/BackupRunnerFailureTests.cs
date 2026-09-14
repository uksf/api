using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupRunnerFailureTests
{
    private readonly BackupRunnerHarness _harness = new();

    [Fact]
    public async Task A_failure_marks_the_run_failed_alerts_and_rethrows()
    {
        _harness.MockMongoDumpService.Setup(x => x.Dump(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UksfException("mongodump failed for 'all'", 500));

        var act = () => _harness.Subject.Run();

        await act.Should().ThrowAsync<UksfException>();
        _harness.Runs[0].State.Should().Be(BackupRunState.Failed);
        _harness.Runs[0].Error.Should().Contain("mongodump failed");
        _harness.MockAlertService.Verify(x => x.Alert(It.Is<string>(y => y.Contains("mongodump failed"))), Times.Once);
    }

    [Fact]
    public async Task A_failed_upload_still_fails_the_run_even_though_the_local_copy_exists()
    {
        _harness.MockGoogleDriveClient.Setup(x => x.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UksfException("Drive upload failed", 500));

        var act = () => _harness.Subject.Run();

        await act.Should().ThrowAsync<UksfException>();
        _harness.MockAlertService.Verify(x => x.Alert(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Staging_is_always_cleaned_up()
    {
        _harness.MockArchiveService.Setup(x => x.WriteArchive(
                                              It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                                              It.IsAny<Stream>(),
                                              It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                                              It.IsAny<CancellationToken>()
                                          )
                )
                .ThrowsAsync(new IOException("disk full"));

        var act = () => _harness.Subject.Run();

        await act.Should().ThrowAsync<IOException>();
        _harness.MockFileSystemProvider.Verify(x => x.DeleteDirectory(It.Is<string>(y => y.Contains("staging-"))), Times.Once);
    }

    [Fact]
    public async Task A_missing_public_key_stops_the_run_before_anything_is_uploaded()
    {
        _harness.MockVariablesService.Setup(x => x.GetVariable("BACKUP_PUBLIC_KEY")).Returns(new DomainVariableItem { Key = "BACKUP_PUBLIC_KEY" });

        var act = () => _harness.Subject.Run();

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("BACKUP_PUBLIC_KEY");
        _harness.MockGoogleDriveClient.Verify(x => x.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_empty_selection_with_no_dumps_fails_rather_than_uploading_an_empty_archive()
    {
        _harness.MockSelectionService.Setup(x => x.GetEntries()).Returns([]);
        _harness.MockMongoDumpService.Setup(x => x.Dump(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var act = () => _harness.Subject.Run();

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("nothing to archive");
    }

    [Fact]
    public async Task A_failure_to_clean_staging_does_not_hide_the_real_error()
    {
        _harness.MockMongoDumpService.Setup(x => x.Dump(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UksfException("mongodump failed", 500));
        _harness.MockFileSystemProvider.Setup(x => x.DeleteDirectory(It.IsAny<string>())).Throws(new IOException("staging in use"));

        var act = () => _harness.Subject.Run();

        (await act.Should().ThrowAsync<UksfException>()).Which.Message.Should().Contain("mongodump failed");
    }
}
