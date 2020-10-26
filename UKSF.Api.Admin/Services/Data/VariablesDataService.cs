using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Admin.Models;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Services.Data;

namespace UKSF.Api.Admin.Services.Data {
    public interface IVariablesDataService : IDataService<VariableItem>, ICachedDataService {
        Task Update(string key, object value);
    }

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
