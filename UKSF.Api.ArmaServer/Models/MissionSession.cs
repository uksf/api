using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum MissionType
{
    MainOp,
    Training,
    SideOp
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
}
