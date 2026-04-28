using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class DomainDevRun : MongoObject
{
    public string RunId { get; set; }
    public string Sqf { get; set; }
    public IReadOnlyList<string> Mods { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DevRunStatus Status { get; set; }
    public List<DevRunLogEntry> Logs { get; set; } = [];
    public bool LogsTruncated { get; set; }
    public string Result { get; set; }
    public string ResultFilePath { get; set; }
    public string FailureDetail { get; set; }
}
