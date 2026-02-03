namespace UKSF.Api.Modpack.Models.Request;

public class InstallWorkshopModRequest
{
    public string SteamId { get; set; }
    public bool RootMod { get; set; }
    public string FolderName { get; set; }
}
