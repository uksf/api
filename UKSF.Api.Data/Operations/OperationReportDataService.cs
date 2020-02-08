using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationReportDataService : CachedDataService<Oprep, IOperationReportDataService>, IOperationReportDataService {
        private readonly IDataCollection dataCollection;

        public OperationReportDataService(IDataCollection dataCollection, IDataEventBus<IOperationReportDataService> dataEventBus) : base(dataCollection, dataEventBus, "oprep") => this.dataCollection = dataCollection;

        public override List<Oprep> Get() {
            List<Oprep> reversed = new List<Oprep>(base.Get());
            reversed.Reverse();
            return reversed;
        }

        public async Task Replace(Oprep request) {
            await dataCollection.Replace(request.id, request);
            Refresh();
        }
    }
}
