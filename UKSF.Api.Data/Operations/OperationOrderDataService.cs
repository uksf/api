using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationOrderDataService : CachedDataService<Opord, IOperationOrderDataService>, IOperationOrderDataService {
        private readonly IDataCollection dataCollection;

        public OperationOrderDataService(IDataCollection dataCollection, IDataEventBus<IOperationOrderDataService> dataEventBus) : base(dataCollection, dataEventBus, "opord") => this.dataCollection = dataCollection;

        public override List<Opord> Get() {
            List<Opord> reversed = new List<Opord>(base.Get());
            reversed.Reverse();
            return reversed;
        }

        public async Task Replace(Opord opord) {
            await dataCollection.Replace(opord.id, opord);
            Refresh();
        }
    }
}
