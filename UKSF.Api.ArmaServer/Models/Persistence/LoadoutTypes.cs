using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class ArmaLoadout
{
    [JsonPropertyName("primaryWeapon")]
    public WeaponSlot PrimaryWeapon { get; set; } = new();

    [JsonPropertyName("secondaryWeapon")]
    public WeaponSlot SecondaryWeapon { get; set; } = new();

    [JsonPropertyName("handgun")]
    public WeaponSlot Handgun { get; set; } = new();

    [JsonPropertyName("uniform")]
    public ContainerSlot Uniform { get; set; } = new();

    [JsonPropertyName("vest")]
    public ContainerSlot Vest { get; set; } = new();

    [JsonPropertyName("backpack")]
    public ContainerSlot Backpack { get; set; } = new();

    [JsonPropertyName("headgear")]
    public string Headgear { get; set; } = string.Empty;

    [JsonPropertyName("facewear")]
    public string Facewear { get; set; } = string.Empty;

    [JsonPropertyName("binoculars")]
    public WeaponSlot Binoculars { get; set; } = new();

    [JsonPropertyName("linkedItems")]
    public LinkedItems LinkedItems { get; set; } = new();
}

public class WeaponSlot
{
    [JsonPropertyName("weapon")]
    public string Weapon { get; set; } = string.Empty;

    [JsonPropertyName("muzzle")]
    public string Muzzle { get; set; } = string.Empty;

    [JsonPropertyName("pointer")]
    public string Pointer { get; set; } = string.Empty;

    [JsonPropertyName("optic")]
    public string Optic { get; set; } = string.Empty;

    [JsonPropertyName("primaryMagazine")]
    public MagazineState PrimaryMagazine { get; set; } = new();

    [JsonPropertyName("secondaryMagazine")]
    public MagazineState SecondaryMagazine { get; set; } = new();

    [JsonPropertyName("bipod")]
    public string Bipod { get; set; } = string.Empty;
}

public class MagazineState
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("ammo")]
    public int Ammo { get; set; }
}

public class ContainerSlot
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<ContainerItem> Items { get; set; } = [];
}

public class ContainerItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "item";

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("ammo")]
    public int? Ammo { get; set; }

    [JsonPropertyName("weapon")]
    public WeaponSlot? Weapon { get; set; }

    [JsonPropertyName("isBackpack")]
    public bool? IsBackpack { get; set; }
}

public class LinkedItems
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = string.Empty;

    [JsonPropertyName("gps")]
    public string Gps { get; set; } = string.Empty;

    [JsonPropertyName("radio")]
    public string Radio { get; set; } = string.Empty;

    [JsonPropertyName("compass")]
    public string Compass { get; set; } = string.Empty;

    [JsonPropertyName("watch")]
    public string Watch { get; set; } = string.Empty;

    [JsonPropertyName("nvg")]
    public string Nvg { get; set; } = string.Empty;
}
