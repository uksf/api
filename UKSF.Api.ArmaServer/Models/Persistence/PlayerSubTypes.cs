using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PlayerVehicleState
{
    [JsonPropertyName("vehicleId")]
    public string VehicleId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    // Polymorphic: cargo roles use int (getCargoIndex), turret roles use int[] (CBA_fnc_turretPath)
    [JsonPropertyName("index")]
    public object Index { get; set; } = -1;
}

public class RadioState
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public int Channel { get; set; }

    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("spatial")]
    public string Spatial { get; set; } = "CENTER";

    [JsonPropertyName("pttIndex")]
    public int PttIndex { get; set; }
}

public class PlayerDiveState
{
    [JsonPropertyName("isDiving")]
    public bool IsDiving { get; set; }

    // Elements 1-32 of the dive state array (gas levels, tissue saturation, decompression, toxicity)
    // Null when not diving
    [JsonPropertyName("rawData")]
    public List<object> RawData { get; set; } = [];
}
