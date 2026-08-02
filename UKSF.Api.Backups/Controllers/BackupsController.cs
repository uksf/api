using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;

namespace UKSF.Api.Backups.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Admin)]
public class BackupsController(
    IBackupSelectionService backupSelectionService,
    IFileTreeService fileTreeService,
    IBackupRunsContext backupRunsContext,
    IBackupRunner backupRunner
) : ControllerBase
{
    [HttpGet("entries")]
    public IEnumerable<DomainBackupEntry> GetEntries()
    {
        return backupSelectionService.GetEntries();
    }

    [HttpPut("entries")]
    public Task<DomainBackupEntry> AddEntry([FromBody] DomainBackupEntry entry)
    {
        return backupSelectionService.AddEntry(entry);
    }

    [HttpPatch("entries")]
    public Task<DomainBackupEntry> UpdateEntry([FromBody] DomainBackupEntry entry)
    {
        return backupSelectionService.UpdateEntry(entry);
    }

    [HttpDelete("entries/{id}")]
    public Task DeleteEntry([FromRoute] string id)
    {
        return backupSelectionService.DeleteEntry(id);
    }

    [HttpGet("runs")]
    public IEnumerable<DomainBackupRun> GetRuns()
    {
        return backupRunsContext.Get().OrderByDescending(x => x.Started).Take(30);
    }

    [HttpPost("run")]
    public Task<DomainBackupRun> RunNow()
    {
        return backupRunner.Start();
    }

    [HttpGet("tree")]
    public IEnumerable<BackupTreeNode> GetTree([FromQuery] string path)
    {
        return string.IsNullOrWhiteSpace(path) ? fileTreeService.GetRoots() : fileTreeService.GetChildren(path);
    }
}
