using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum MissionType
{
    MainOp,
    Training,
    SideOp
}

public class PlayerPresence
{
    public string Uid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Connected { get; set; }
    public DateTime? Disconnected { get; set; }
}

public class MissionSession : MongoObject
{
    public string Mission { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public MissionType Type { get; set; }
    public DateTime Date { get; set; }
    public DateTime FirstBatchReceived { get; set; }
    public DateTime LastBatchReceived { get; set; }
    public int TotalBatchesReceived { get; set; }

    // Lifecycle — set from mission_started/mission_ended events
    public DateTime? MissionStarted { get; set; }
    public DateTime? MissionEnded { get; set; }
    public double? DurationSeconds { get; set; }

    // Player presence — connect/disconnect events
    public List<PlayerPresence> PlayerPresence { get; set; } = [];
}
