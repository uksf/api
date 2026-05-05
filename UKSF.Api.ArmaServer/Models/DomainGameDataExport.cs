using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class DomainGameDataExport : MongoObject
{
    public string ModpackVersion { get; set; }
    public string GameVersion { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public GameDataExportStatus Status { get; set; }
    public bool HasConfig { get; set; }
    public bool HasCbaSettings { get; set; }
    public bool HasCbaSettingsReference { get; set; }
    public string FailureDetail { get; set; }
}
