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
    public List<object> TurretWeapons { get; set; } = [];

    [JsonPropertyName("turretMagazines")]
    public List<object> TurretMagazines { get; set; } = [];

    [JsonPropertyName("pylonLoadout")]
    public List<object> PylonLoadout { get; set; } = [];

    [JsonPropertyName("logistics")]
    public double[] Logistics { get; set; } = [];

    [JsonPropertyName("attached")]
    public List<object> Attached { get; set; } = [];

    [JsonPropertyName("rackChannels")]
    public int[] RackChannels { get; set; } = [];

    [JsonPropertyName("aceCargo")]
    public List<object> AceCargo { get; set; } = [];

    [JsonPropertyName("inventory")]
    public object Inventory { get; set; } = new object[] { };

    [JsonPropertyName("aceFortify")]
    public object AceFortify { get; set; } = new object[] { };

    [JsonPropertyName("aceMedical")]
    public object AceMedical { get; set; } = new object[] { };

    [JsonPropertyName("aceRepair")]
    public object AceRepair { get; set; } = new object[] { };

    [JsonPropertyName("customName")]
    public string CustomName { get; set; } = string.Empty;
}
