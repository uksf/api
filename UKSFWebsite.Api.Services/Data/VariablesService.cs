using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data {
    public class VariablesService : CachedDataService<VariableItem>, IVariablesService {
        public VariablesService(IMongoDatabase database) : base(database, "variables") { }

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

    public static class VariablesServiceConverter {
        public static string AsString(this VariableItem variable) => variable.item.ToString();
        public static bool AsBool(this VariableItem variable) => bool.Parse(variable.item.ToString());
        public static ulong AsUlong(this VariableItem variable) => ulong.Parse(variable.item.ToString());
        public static string[] AsArray(this VariableItem variable, Func<string, string> predicate = null) {
            string itemString = variable.item.ToString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            string[] items = itemString.Split(",");
            return predicate != null ? items.Select(predicate).ToArray() : items;
        }
    }
}
