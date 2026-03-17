using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PlayerVehicleState
{
    [JsonPropertyName("vehicleId")]
    public string VehicleId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; } = -1;
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
}
