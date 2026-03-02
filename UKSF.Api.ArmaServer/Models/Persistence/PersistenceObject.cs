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
    public object[] TurretWeapons { get; set; } = [];

    [JsonPropertyName("turretMagazines")]
    public object[] TurretMagazines { get; set; } = [];

    [JsonPropertyName("pylonLoadout")]
    public object[] PylonLoadout { get; set; } = [];

    [JsonPropertyName("logistics")]
    public double[] Logistics { get; set; } = [];

    [JsonPropertyName("attached")]
    public object[] Attached { get; set; } = [];

    [JsonPropertyName("rackChannels")]
    public object[] RackChannels { get; set; } = [];

    [JsonPropertyName("aceCargo")]
    public object[] AceCargo { get; set; } = [];

    [JsonPropertyName("inventory")]
    public object[][] Inventory { get; set; } = [];

    [JsonPropertyName("aceFortify")]
    public object[] AceFortify { get; set; } = [];

    [JsonPropertyName("aceMedical")]
    public object[] AceMedical { get; set; } = [];

    [JsonPropertyName("aceRepair")]
    public object[] AceRepair { get; set; } = [];

    [JsonPropertyName("customName")]
    public string CustomName { get; set; } = string.Empty;
}
