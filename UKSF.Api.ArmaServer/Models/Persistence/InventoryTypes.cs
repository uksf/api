using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class CargoSlot
{
    [JsonPropertyName("classNames")]
    public string[] ClassNames { get; set; } = [];

    [JsonPropertyName("counts")]
    public int[] Counts { get; set; } = [];
}

public class InventoryContainer
{
    [JsonPropertyName("weapons")]
    public CargoSlot Weapons { get; set; } = new();

    [JsonPropertyName("magazines")]
    public CargoSlot Magazines { get; set; } = new();

    [JsonPropertyName("items")]
    public CargoSlot Items { get; set; } = new();

    [JsonPropertyName("backpacks")]
    public CargoSlot Backpacks { get; set; } = new();
}

public class AceCargoEntry
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("cargo")]
    public List<AceCargoEntry> Cargo { get; set; } = [];

    [JsonPropertyName("inventory")]
    public InventoryContainer Inventory { get; set; } = new();

    [JsonPropertyName("customName")]
    public string CustomName { get; set; } = string.Empty;
}
