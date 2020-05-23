using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IDischargeDataService : IDataService<DischargeCollection, IDischargeDataService>, ICachedDataService { }
}
