using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace UKSFWebsite.Api.Data {
    public abstract class CachedDataService<T> : DataService<T> {
        protected List<T> Collection;

        protected CachedDataService(IMongoDatabase database, string collectionName) : base(database, collectionName) { }

        // ReSharper disable once MemberCanBeProtected.Global // Used as dynamic call in startup
        public void Refresh() {
            Collection = null;
            Get();
        }

        public override List<T> Get() {
            if (Collection != null) {
                return Collection;
            }

            Collection = base.Get();
            return Collection;
        }

        public override List<T> Get(Func<T, bool> predicate) {
            if (Collection == null) Get();
            return Collection.Where(predicate).ToList();
        }

        public override T GetSingle(string id) {
            if (Collection == null) Get();
            return Collection.FirstOrDefault(x => GetIdValue(x) == id);
        }

        public override T GetSingle(Func<T, bool> predicate) {
            if (Collection == null) Get();
            return Collection.FirstOrDefault(predicate);
        }

        public override async Task Add(T data) {
            await base.Add(data);
            Refresh();
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            Refresh();
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            Refresh();
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            Refresh();
        }
    }
}
