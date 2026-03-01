using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class MissionStats : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public Dictionary<string, int> EventCounts { get; set; } = new();
}
