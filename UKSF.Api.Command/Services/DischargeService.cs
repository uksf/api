using UKSF.Api.Base.Context;
using UKSF.Api.Command.Context;

namespace UKSF.Api.Command.Services {
    public interface IDischargeService : IDataBackedService<IDischargeDataService> { }

    public class DischargeService : DataBackedService<IDischargeDataService>, IDischargeService {
        public DischargeService(IDischargeDataService data) : base(data) { }
    }
}
