using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PersistenceObject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public double[] Position { get; set; } = [];

    [JsonPropertyName("vectorDirUp")]
    public double[][] VectorDirUp { get; set; } = [];

    [JsonPropertyName("damage")]
    public double Damage { get; set; }

    [JsonPropertyName("fuel")]
    public double Fuel { get; set; }

    [JsonPropertyName("turretWeapons")]
    public List<TurretWeaponsEntry> TurretWeapons { get; set; } = [];

    [JsonPropertyName("turretMagazines")]
    public List<TurretMagazineEntry> TurretMagazines { get; set; } = [];

    [JsonPropertyName("pylonLoadout")]
    public List<PylonEntry> PylonLoadout { get; set; } = [];

    [JsonPropertyName("logistics")]
    public double[] Logistics { get; set; } = [];

    [JsonPropertyName("attached")]
    public List<AttachedObject> Attached { get; set; } = [];

    [JsonPropertyName("rackChannels")]
    public int[] RackChannels { get; set; } = [];

    [JsonPropertyName("aceCargo")]
    public List<AceCargoEntry> AceCargo { get; set; } = [];

    [JsonPropertyName("inventory")]
    public InventoryContainer Inventory { get; set; } = new();

    [JsonPropertyName("aceFortify")]
    public AceFortifyState AceFortify { get; set; } = new();

    [JsonPropertyName("aceMedical")]
    public ObjectMedicalState AceMedical { get; set; } = new();

    [JsonPropertyName("aceRepair")]
    public ObjectRepairState AceRepair { get; set; } = new();

    [JsonPropertyName("customName")]
    public string CustomName { get; set; } = string.Empty;
}
