using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class WeaponStats
{
    public int Shots { get; set; }
    public int Hits { get; set; }
    public Dictionary<string, int> FireModes { get; set; } = new();
    public double TotalEngagementDistance2D { get; set; }
    public double TotalEngagementDistance3D { get; set; }
    public double MaxEngagementDistance2D { get; set; }
    public int HitCount { get; set; } // For computing average engagement distance
}

public class KillStats
{
    public int Direct { get; set; }
    public int Indirect { get; set; } // Chain reaction / cookoff kills
    public int Assists { get; set; }
    public double TotalAssistDamage { get; set; }
}

public class PlayerMissionStats : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public string PlayerUid { get; set; } = string.Empty;

    // Shots and hits
    public int TotalShots { get; set; }
    public int TotalHits { get; set; }
    public Dictionary<string, WeaponStats> WeaponBreakdown { get; set; } = new();

    // Hit details
    public Dictionary<string, int> BodyPartHits { get; set; } = new();
    public Dictionary<string, int> HitsByTargetType { get; set; } = new(); // infantry, vehicle, static

    // Kills and assists
    public KillStats Kills { get; set; } = new();
    public Dictionary<string, int> KillsByTargetType { get; set; } = new(); // infantry, vehicle, static

    // Damage
    public double TotalDamageDealt { get; set; } // From standalone damage events
    public int TimesWounded { get; set; }
    public Dictionary<string, int> WoundsByBodyPart { get; set; } = new();
    public Dictionary<string, int> WoundsByDamageType { get; set; } = new();

    // Travel
    public double DistanceOnFoot { get; set; }
    public double DistanceInVehicle { get; set; }

    // Vehicle
    public double TotalFuelConsumed { get; set; }

    // Explosives
    public int ExplosivesPlaced { get; set; }

    // Health
    public int TimesUnconscious { get; set; }
}
