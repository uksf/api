using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Admin.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Admin.Context {
    public interface IVariablesContext : IMongoContext<VariableItem>, ICachedMongoContext {
        Task Update(string key, object value);
    }

    public class VariablesContext : CachedMongoContext<VariableItem>, IVariablesContext {
        public VariablesContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<VariableItem> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "variables") { }

        public override VariableItem GetSingle(string key) {
            return base.GetSingle(x => x.Key == key.Keyify());
        }

        public async Task Update(string key, object value) {
            VariableItem variableItem = GetSingle(key);
            if (variableItem == null) throw new KeyNotFoundException($"Variable Item with key '{key}' does not exist");
            await base.Update(variableItem.Id, nameof(variableItem.Item), value);
        }

        public override async Task Delete(string key) {
            VariableItem variableItem = GetSingle(key);
            if (variableItem == null) throw new KeyNotFoundException($"Variable Item with key '{key}' does not exist");
            await base.Delete(variableItem);
        }

        protected override void SetCache(IEnumerable<VariableItem> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Key).ToList();
            }
        }
    }
}
