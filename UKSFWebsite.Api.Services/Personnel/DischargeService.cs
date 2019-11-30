using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class DischargeService : DataBackedService<IDischargeDataService>, IDischargeService {
        public DischargeService(IDischargeDataService data) : base(data) { }
    }
}
