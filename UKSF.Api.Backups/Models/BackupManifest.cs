namespace UKSF.Api.Backups.Models;

public class BackupManifest
{
    public DateTime CreatedUtc { get; set; }
    public string MachineName { get; set; }
    public int FileCount { get; set; }
    public long RawBytes { get; set; }
    public List<BackupManifestEntry> Entries { get; set; } = [];
    public List<BackupSkip> Skips { get; set; } = [];
}

public class BackupManifestEntry
{
    public string Path { get; set; }
    public BackupEntryType EntryType { get; set; }
    public bool Recursive { get; set; }
    public List<string> Excludes { get; set; } = [];
    public int FileCount { get; set; }
    public long RawBytes { get; set; }
}

public class BackupSkip
{
    public string Path { get; set; }
    public string Reason { get; set; }
}
