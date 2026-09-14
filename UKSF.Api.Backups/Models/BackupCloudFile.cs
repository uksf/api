namespace UKSF.Api.Backups.Models;

public class BackupCloudFile
{
    public string Id { get; set; }
    public string Name { get; set; }
    public long Bytes { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class BackupCloudQuota
{
    public long LimitBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes => Math.Max(0, LimitBytes - UsedBytes);
}
