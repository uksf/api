using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Events.Data;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Data {
    public abstract class CachedDataService<T> : DataService<T> {
        protected List<T> Collection;

        protected CachedDataService(IMongoDatabase database, IEventBus dataEventBus, string collectionName) : base(database, dataEventBus, collectionName) { }

        // ReSharper disable once MemberCanBeProtected.Global - Used in dynamic call, do not change to protected!
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
            CachedDataEvent(DataEventFactory.Create(DataEventType.ADD, GetIdValue(data), data));
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            Refresh();
            CachedDataEvent(DataEventFactory.Create(DataEventType.UPDATE, id));
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            Refresh();
            CachedDataEvent(DataEventFactory.Create(DataEventType.UPDATE, id));
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            Refresh();
            CachedDataEvent(DataEventFactory.Create(DataEventType.DELETE, id));
        }

        protected virtual void CachedDataEvent(DataEventModel dataEvent) {
            base.DataEvent(dataEvent);
        }

        protected override void DataEvent(DataEventModel dataEvent) { }
    }
}
