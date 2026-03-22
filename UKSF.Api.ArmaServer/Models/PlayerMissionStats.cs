using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class AmmoStats
{
    public int Shots { get; set; }
    public int Hits { get; set; }
    public Dictionary<string, int> BodyPartHits { get; set; } = new();
    public double EngagementDistanceSum { get; set; }
    public double MinEngagementDistance { get; set; } = double.MaxValue;
    public double MaxEngagementDistance { get; set; }
}

public class WeaponStats
{
    public int Shots { get; set; }
    public int Hits { get; set; }
    public Dictionary<string, AmmoStats> AmmoBreakdown { get; set; } = new();
    public double EngagementDistanceSum { get; set; }
    public double MinEngagementDistance { get; set; } = double.MaxValue;
    public double MaxEngagementDistance { get; set; }
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

    // Performance — rolling stats updated from performance events, P1 computed at mission end
    public int? FpsMin { get; set; }
    public int? FpsMax { get; set; }
    public double? FpsAverage { get; set; }
    public int? FpsP1 { get; set; } // 1st percentile (1% low)
    public int FpsSampleCount { get; set; } // For rolling average computation
    public double FpsSampleSum { get; set; } // For rolling average computation

    // Health
    public int TimesUnconscious { get; set; }
}
