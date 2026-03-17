using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class TurretWeaponsEntry
{
    [JsonPropertyName("turretPath")]
    public int[] TurretPath { get; set; } = [];

    [JsonPropertyName("weapons")]
    public string[] Weapons { get; set; } = [];
}

public class TurretMagazineEntry
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("turretPath")]
    public int[] TurretPath { get; set; } = [];

    [JsonPropertyName("ammoCount")]
    public int AmmoCount { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("ammo")]
    public int Ammo { get; set; }
}

public class PylonEntry
{
    [JsonPropertyName("magazine")]
    public string Magazine { get; set; } = string.Empty;

    [JsonPropertyName("ammo")]
    public int Ammo { get; set; }
}

public class AttachedObject
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public double[] Offset { get; set; } = [];
}

public class AceFortifyState
{
    [JsonPropertyName("isAceFortification")]
    public bool IsAceFortification { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "WEST";
}

public class ObjectMedicalState
{
    [JsonPropertyName("medicClass")]
    public int MedicClass { get; set; }

    [JsonPropertyName("medicalVehicle")]
    public bool MedicalVehicle { get; set; }

    [JsonPropertyName("medicalFacility")]
    public bool MedicalFacility { get; set; }
}

public class ObjectRepairState
{
    [JsonPropertyName("repairVehicle")]
    public int RepairVehicle { get; set; }

    [JsonPropertyName("repairFacility")]
    public int RepairFacility { get; set; }
}
