using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PlayerRedeployData
{
    [JsonPropertyName("position")]
    public double[] Position { get; set; } = [];

    [JsonPropertyName("vehicleState")]
    public PlayerVehicleState VehicleState { get; set; } = new();

    [JsonPropertyName("direction")]
    public double Direction { get; set; }

    [JsonPropertyName("animation")]
    public string Animation { get; set; } = string.Empty;

    [JsonPropertyName("loadout")]
    public ArmaLoadout Loadout { get; set; } = new();

    [JsonPropertyName("damage")]
    public double Damage { get; set; }

    [JsonPropertyName("aceMedical")]
    public AceMedicalState AceMedical { get; set; } = new();

    [JsonPropertyName("earplugs")]
    public bool Earplugs { get; set; }

    [JsonPropertyName("attachedItems")]
    public string[] AttachedItems { get; set; } = [];

    [JsonPropertyName("radios")]
    public List<RadioState> Radios { get; set; } = [];

    [JsonPropertyName("diveState")]
    public PlayerDiveState DiveState { get; set; } = new();
}
