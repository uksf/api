namespace UKSF.Api.Backups.Models;

public class BackupTreeNode
{
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; set; }
    public bool HasChildren { get; set; }
}
