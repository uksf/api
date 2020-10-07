using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IReleasesDataService : IDataService<ModpackRelease>, ICachedDataService  { }
}
