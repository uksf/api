using UKSF.Api.Core.Models;

namespace UKSF.Api.Backups.Models;

public enum BackupRunState
{
    Running,
    Success,
    Failed
}

public class DomainBackupRun : MongoObject
{
    public DateTime Started { get; set; }
    public DateTime? Finished { get; set; }
    public BackupRunState State { get; set; }
    public int FileCount { get; set; }
    public long RawBytes { get; set; }
    public long ArchiveBytes { get; set; }
    public string ArchiveName { get; set; }
    public string LocalPath { get; set; }
    public string DriveFileId { get; set; }
    public string Error { get; set; }
    public List<BackupSkip> Skips { get; set; } = [];
    public List<string> Databases { get; set; } = [];
}
