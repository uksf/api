namespace UKSF.Api.Modpack.Models.Response;

public class WorkshopModResponse
{
    public string Id { get; set; }
    public string SteamId { get; set; }
    public string Name { get; set; }
    public bool RootMod { get; set; }
    public string Status { get; set; }
    public List<string> Pbos { get; set; } = [];
    public string LastUpdatedLocally { get; set; }
    public string ModpackVersionFirstAdded { get; set; }
    public string ModpackVersionLastUpdated { get; set; }
    public List<string> CustomFilesList { get; set; } = [];
    public string StatusMessage { get; set; }
    public string ErrorMessage { get; set; }
}

public class WorkshopModUpdatedDateResponse
{
    public string UpdatedDate { get; set; }
}
