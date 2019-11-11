using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class DischargeService : IDischargeService {
        private readonly IDischargeDataService data;

        public DischargeService(IDischargeDataService data) => this.data = data;

        public IDischargeDataService Data() => data;
    }
}
