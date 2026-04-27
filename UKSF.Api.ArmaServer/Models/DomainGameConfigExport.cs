using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class DomainGameConfigExport : MongoObject
{
    public string ModpackVersion { get; set; }
    public string GameVersion { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ConfigExportStatus Status { get; set; }
    public string FilePath { get; set; }
    public string FailureDetail { get; set; }
}
