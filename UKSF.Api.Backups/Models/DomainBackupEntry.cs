using UKSF.Api.Core.Models;

namespace UKSF.Api.Backups.Models;

public enum BackupEntryType
{
    Folder,
    File
}

public class DomainBackupEntry : MongoObject
{
    public string Path { get; set; }
    public BackupEntryType EntryType { get; set; }
    public bool Recursive { get; set; } = true;

    /// <summary>File name patterns. When set, only matching files are taken, at any depth under the folder.</summary>
    public List<string> IncludePatterns { get; set; } = [];

    public List<string> Excludes { get; set; } = [];
    public bool Enabled { get; set; } = true;
}
