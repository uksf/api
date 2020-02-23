using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Admin;
using UKSF.Common;

namespace UKSF.Api.Data.Admin {
    public class VariablesDataService : CachedDataService<VariableItem, IVariablesDataService>, IVariablesDataService {
        public VariablesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IVariablesDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "variables") { }

        public override List<VariableItem> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.key).ToList();
            return Collection;
        }

        public override VariableItem GetSingle(string key) {
            return base.GetSingle(x => x.key == key.Keyify());
        }

        public async Task Update(string key, object value) {
            VariableItem variableItem = GetSingle(key);
            if (variableItem == null) throw new KeyNotFoundException($"Variable Item with key '{key}' does not exist");
            await base.Update(variableItem.id, "item", value);
        }

        public override async Task Delete(string key) {
            VariableItem variableItem = GetSingle(key);
            if (variableItem == null) throw new KeyNotFoundException($"Variable Item with key '{key}' does not exist");
            await base.Delete(variableItem.id);
        }
    }
}
