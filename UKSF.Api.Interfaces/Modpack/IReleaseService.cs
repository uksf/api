using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack {
    public interface IReleaseService : IDataBackedService<IReleasesDataService> {
        Task MakeDraftRelease(string version, ModpackBuild build);
        Task UpdateDraft(ModpackRelease release);
        Task PublishRelease(string version);
        ModpackRelease GetRelease(string version);
        Task AddHistoricReleases(List<ModpackRelease> releases);
    }
}
