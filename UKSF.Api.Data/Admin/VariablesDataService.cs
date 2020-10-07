using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Admin;
using UKSF.Common;

namespace UKSF.Api.Data.Admin {
    public class VariablesDataService : CachedDataService<VariableItem>, IVariablesDataService {
        public VariablesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<VariableItem> dataEventBus) : base(dataCollectionFactory, dataEventBus, "variables") { }

        protected override void SetCache(IEnumerable<VariableItem> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.key).ToList();
            }
        }

        public override VariableItem GetSingle(string key) {
            return base.GetSingle(x => x.key == key.Keyify());
        }

        public async Task Update(string key, object value) {
            VariableItem variableItem = GetSingle(key);
            if (variableItem == null) throw new KeyNotFoundException($"Variable Item with key '{key}' does not exist");
            await base.Update(variableItem.id, nameof(variableItem.item), value);
        }

        public override async Task Delete(string key) {
            VariableItem variableItem = GetSingle(key);
            if (variableItem == null) throw new KeyNotFoundException($"Variable Item with key '{key}' does not exist");
            await base.Delete(variableItem);
        }
    }
}
