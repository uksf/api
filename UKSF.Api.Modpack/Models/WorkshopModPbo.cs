namespace UKSF.Api.Modpack.Models;

/// <summary>A PBO found outside the mod's addons folder, with its folder relative to the mod root.</summary>
public class WorkshopModPbo
{
    public string Name { get; set; }
    public string Folder { get; set; }
}
