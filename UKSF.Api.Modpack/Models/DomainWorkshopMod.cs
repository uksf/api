using UKSF.Api.Core.Models;

namespace UKSF.Api.Modpack.Models;

public class DomainWorkshopMod : MongoObject
{
    public string SteamId { get; set; }
    public bool RootMod { get; set; }
}
