using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Data {
    public abstract class CachedDataService<T, TData> : DataService<T, TData> {
        protected List<T> Collection;

        protected CachedDataService(IDataCollection dataCollection, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataCollection, dataEventBus, collectionName) { }

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
            return Collection.FirstOrDefault(x => x.GetIdValue() == id);
        }

        public override T GetSingle(Func<T, bool> predicate) {
            if (Collection == null) Get();
            return Collection.FirstOrDefault(predicate);
        }

        public override async Task Add(T data) {
            await base.Add(data);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }

        protected virtual void CachedDataEvent(DataEventModel<TData> dataEvent) {
            base.DataEvent(dataEvent);
        }

        protected override void DataEvent(DataEventModel<TData> dataEvent) { }
    }
}