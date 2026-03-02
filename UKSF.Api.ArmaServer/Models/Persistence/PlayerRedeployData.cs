using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PlayerRedeployData
{
    [JsonPropertyName("position")]
    public double[] Position { get; set; } = [];

    [JsonPropertyName("vehicleState")]
    public object[] VehicleState { get; set; } = [];

    [JsonPropertyName("direction")]
    public double Direction { get; set; }

    [JsonPropertyName("animation")]
    public string Animation { get; set; } = string.Empty;

    [JsonPropertyName("loadout")]
    public object[] Loadout { get; set; } = [];

    [JsonPropertyName("damage")]
    public double Damage { get; set; }

    [JsonPropertyName("aceMedical")]
    public object[] AceMedical { get; set; } = [];

    [JsonPropertyName("earplugs")]
    public bool Earplugs { get; set; }

    [JsonPropertyName("attachedItems")]
    public string[] AttachedItems { get; set; } = [];

    [JsonPropertyName("radios")]
    public object[] Radios { get; set; } = [];

    [JsonPropertyName("diveState")]
    public object[] DiveState { get; set; } = [];
}
