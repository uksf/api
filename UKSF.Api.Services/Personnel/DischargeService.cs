using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class DischargeService : DataBackedService<IDischargeDataService>, IDischargeService {
        public DischargeService(IDischargeDataService data) : base(data) { }
    }
}
