﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Utility;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Data.Utility {
    public class VariablesDataService : CachedDataService<VariableItem>, IVariablesDataService {
        public VariablesDataService(IMongoDatabase database) : base(database, "variables") { }

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
            await Database.GetCollection<VariableItem>(DatabaseCollection).UpdateOneAsync(x => x.key == key.Keyify(), update);
            Refresh();
        }

        public override async Task Update(string key, UpdateDefinition<VariableItem> update) {
            await Database.GetCollection<VariableItem>(DatabaseCollection).UpdateOneAsync(x => x.key == key.Keyify(), update);
            Refresh();
        }

        public override async Task Delete(string key) {
            await Database.GetCollection<VariableItem>(DatabaseCollection).DeleteOneAsync(x => x.key == key.Keyify());
            Refresh();
        }
    }
}