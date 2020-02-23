using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Common;

namespace UKSF.Api.Data {
    public abstract class CachedDataService<T, TData> : DataService<T, TData> {
        private List<T> collection;
        private readonly object lockObject = new object();

        protected CachedDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }

        public List<T> Collection {
            get => collection;
            protected set {
                lock (lockObject) collection = value;
            }
        }

        // ReSharper disable once MemberCanBeProtected.Global - Used in dynamic call, do not change to protected! // TODO: Stop using this in dynamic call, switch to register or something less........dynamic
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

        public override async Task UpdateMany(Func<T, bool> predicate, UpdateDefinition<T> update) {
            List<T> items = Get(predicate);
            await base.UpdateMany(predicate, update);
            Refresh();
            items.ForEach(x => CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, x.GetIdValue())));
        }

        public override async Task Replace(T item) {
            await base.Replace(item);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, item.GetIdValue()));
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }

        public override async Task DeleteMany(Func<T, bool> predicate) {
            List<T> items = Get(predicate);
            await base.DeleteMany(predicate);
            Refresh();
            items.ForEach(x => CachedDataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, x.GetIdValue())));
        }

        protected virtual void CachedDataEvent(DataEventModel<TData> dataEvent) {
            base.DataEvent(dataEvent);
        }

        protected override void DataEvent(DataEventModel<TData> dataEvent) { }
    }
}
