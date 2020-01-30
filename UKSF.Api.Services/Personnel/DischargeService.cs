using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class DischargeService : IDischargeService {
        private readonly IDischargeDataService data;

        public DischargeService(IDischargeDataService data) => this.data = data;

        public IDischargeDataService Data() => data;
    }
}
