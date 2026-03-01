using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class WeaponStats
{
    public int Shots { get; set; }
    public int Hits { get; set; }
    public Dictionary<string, int> FireModes { get; set; } = new();
}

public class PlayerMissionStats : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public string PlayerUid { get; set; } = string.Empty;
    public int TotalShots { get; set; }
    public int TotalHits { get; set; }
    public Dictionary<string, WeaponStats> WeaponBreakdown { get; set; } = new();
    public Dictionary<string, int> BodyPartHits { get; set; } = new();
    public int TotalDistance { get; set; }
}
