using UKSF.Api.Base.Services.Data;
using UKSF.Api.Personnel.Services.Data;

namespace UKSF.Api.Personnel.Services {
    public interface IDischargeService : IDataBackedService<IDischargeDataService> { }

    public class DischargeService : DataBackedService<IDischargeDataService>, IDischargeService {
        public DischargeService(IDischargeDataService data) : base(data) { }
    }
}
