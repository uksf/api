using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Admin;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.Data.Admin {
    public class VariablesDataService : CachedDataService<VariableItem, IVariablesDataService>, IVariablesDataService {
        private readonly IDataCollection dataCollection;
        
        public VariablesDataService(IDataCollection dataCollection, IDataEventBus<IVariablesDataService> dataEventBus) : base(dataCollection, dataEventBus, "variables") => this.dataCollection = dataCollection;

        public override List<VariableItem> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.key).ToList();
            return Collection;
        }

        public override VariableItem GetSingle(string key) {
            return base.GetSingle(x => x.key == key.Keyify());
        }

        public async Task Update(string key, object value) {
            UpdateDefinition<VariableItem> update = value == null ? Builders<VariableItem>.Update.Unset("item") : Builders<VariableItem>.Update.Set("item", value);
            await dataCollection.Update(key.Keyify(), update);
            Refresh();
        }

        public override async Task Update(string key, UpdateDefinition<VariableItem> update) {
            await dataCollection.Update(key.Keyify(), update);
            Refresh();
        }

        public override async Task Delete(string key) {
            await dataCollection.Delete<VariableItem>(key.Keyify());
            Refresh();
        }
    }
}
