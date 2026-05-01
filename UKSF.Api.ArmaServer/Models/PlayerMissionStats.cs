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
    public int Indirect { get; set; }
}

public class KillTargetTypeStats
{
    public int Count { get; set; }
    public Dictionary<string, int> Types { get; set; } = new();
}

public class KillWeaponStats
{
    public int Count { get; set; }
    public Dictionary<string, int> Ammo { get; set; } = new();
}

public class PlayerMissionStats : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public string PlayerUid { get; set; } = string.Empty;

    // Shots and hits
    public int TotalShots { get; set; }
    public int TotalHits { get; set; }
    public Dictionary<string, WeaponStats> WeaponBreakdown { get; set; } = new();

    // Shots and hits by ammo category (ballistic / explosive / other)
    public int BallisticShots { get; set; }
    public int BallisticHits { get; set; }
    public int ExplosiveShots { get; set; }
    public int ExplosiveHits { get; set; }
    public int OtherShots { get; set; }
    public int OtherHits { get; set; }

    // Hit details
    public Dictionary<string, int> BodyPartHits { get; set; } = new();
    public Dictionary<string, int> HitsByTargetType { get; set; } = new();

    // Kills
    public KillStats Kills { get; set; } = new();
    public Dictionary<string, KillTargetTypeStats> KillsByTargetType { get; set; } = new();
    public Dictionary<string, KillWeaponStats> KillsByWeapon { get; set; } = new();

    // Damage
    public double TotalDamageDealt { get; set; } // From standalone damage events
    public Dictionary<string, double> DamageDealtByAmmo { get; set; } = new();
    public int TimesWounded { get; set; }
    public Dictionary<string, int> WoundsByBodyPart { get; set; } = new();
    public Dictionary<string, int> WoundsByDamageType { get; set; } = new();

    // Travel
    public double DistanceOnFoot { get; set; }
    public double DistanceInVehicle { get; set; }

    // Vehicle
    public double TotalFuelLitres { get; set; }

    // Explosives
    public int ExplosivesPlaced { get; set; }

    // Performance — rolling stats updated from performance events, P1 computed at mission end
    public int? FpsMin { get; set; }
    public int? FpsMax { get; set; }
    public double? FpsAverage { get; set; }
    public int? FpsP1 { get; set; } // 1st percentile (1% low)

    // Health
    public int TimesUnconscious { get; set; }
}
