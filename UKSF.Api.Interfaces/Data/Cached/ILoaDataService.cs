using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface ILoaDataService : IDataService<Loa, ILoaDataService>, ICachedDataService { }
}
