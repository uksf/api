using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Tests.Services;

/// <summary>Shared wiring for the runner tests: every dependency mocked, nothing touches disk, Mongo, Drive or Discord.</summary>
public class BackupRunnerHarness
{
    public static readonly DateTime Now = new(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc);

    public readonly Mock<IBackupAlertService> MockAlertService = new();
    public readonly Mock<IBackupArchiveService> MockArchiveService = new();
    public readonly Mock<IBackupEncryptionService> MockEncryptionService = new();
    public readonly Mock<IFileSystemProvider> MockFileSystemProvider = new();
    public readonly Mock<IGoogleDriveClient> MockGoogleDriveClient = new();
    public readonly Mock<IMongoDumpService> MockMongoDumpService = new();
    public readonly Mock<IBackupRetentionService> MockRetentionService = new();
    public readonly Mock<IBackupRunsContext> MockRunsContext = new();
    public readonly Mock<IBackupSelectionService> MockSelectionService = new();
    public readonly Mock<IVariablesService> MockVariablesService = new();
    public readonly List<DomainBackupRun> Runs = [];
    public readonly BackupRunner Subject;

    public BackupRunnerHarness()
    {
        var mockClock = new Mock<IClock>();
        mockClock.Setup(x => x.UtcNow()).Returns(Now);

        MockRunsContext.Setup(x => x.Add(It.IsAny<DomainBackupRun>()))
                       .Callback((DomainBackupRun run) =>
                           {
                               Runs.Add(run);
                               StateWhenAdded = run.State;
                           }
                       )
                       .Returns(Task.CompletedTask);
        MockRunsContext.Setup(x => x.Replace(It.IsAny<DomainBackupRun>())).Returns(Task.CompletedTask);

        MockVariablesService.Setup(x => x.GetVariable(It.IsAny<string>())).Returns((string key) => new DomainVariableItem { Key = key });
        GivenVariable("BACKUP_PUBLIC_KEY", "-----BEGIN PUBLIC KEY-----");

        MockSelectionService.Setup(x => x.GetEntries()).Returns([new DomainBackupEntry { Path = @"C:\Server\Nginx", EntryType = BackupEntryType.Folder }]);
        MockMongoDumpService.Setup(x => x.Dump(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(
                                [
                                    new MongoDumpFile
                                    {
                                        Database = "all",
                                        Path = @"E:\Backups\staging\mongo\all.archive.gz",
                                        Bytes = 500
                                    }
                                ]
                            );

        MockArchiveService
            .Setup(x => x.WriteArchive(
                       It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                       It.IsAny<Stream>(),
                       It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                       It.IsAny<CancellationToken>()
                   )
            )
            .ReturnsAsync(
                new BackupManifest
                {
                    FileCount = 12,
                    RawBytes = 2048,
                    Skips = [new BackupSkip { Path = @"C:\gone", Reason = "gone" }]
                }
            );

        MockFileSystemProvider.Setup(x => x.Create(It.IsAny<string>())).Returns(() => new MemoryStream());
        MockFileSystemProvider.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream());
        MockFileSystemProvider.Setup(x => x.GetFileSize(It.IsAny<string>())).Returns(1024);

        MockGoogleDriveClient.Setup(x => x.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                             .ReturnsAsync(new BackupCloudFile { Id = "drive-id" });

        Subject = new BackupRunner(
            MockRunsContext.Object,
            MockSelectionService.Object,
            MockMongoDumpService.Object,
            MockArchiveService.Object,
            MockEncryptionService.Object,
            MockRetentionService.Object,
            MockGoogleDriveClient.Object,
            MockFileSystemProvider.Object,
            MockAlertService.Object,
            MockVariablesService.Object,
            mockClock.Object,
            new Mock<IUksfLogger>().Object
        );
    }

    public BackupRunState StateWhenAdded { get; private set; }

    public void GivenVariable(string key, string value)
    {
        MockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    public void GivenArchiveManifest(BackupManifest manifest)
    {
        MockArchiveService.Setup(x => x.WriteArchive(
                                     It.IsAny<IReadOnlyList<DomainBackupEntry>>(),
                                     It.IsAny<Stream>(),
                                     It.IsAny<IReadOnlyList<BackupWalkFile>>(),
                                     It.IsAny<CancellationToken>()
                                 )
                          )
                          .ReturnsAsync(manifest);
    }
}
