using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Modpack;

namespace UKSF.Api.Services.Modpack {
    public class ReleaseService : DataBackedService<IReleasesDataService>, IReleaseService {
        public ReleaseService(IReleasesDataService data) : base(data) { }


    }
}
