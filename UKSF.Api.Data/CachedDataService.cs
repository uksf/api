using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Common;

namespace UKSF.Api.Data {
    public class CachedDataService<T, TData> : DataServiceBase<T, TData>, IDataService<T, TData>, ICachedDataService {
        private List<T> collection;
        protected readonly object LockObject = new object();

        protected CachedDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }

        public virtual List<T> Collection {
            get => collection;
            protected set {
                lock (LockObject) collection = value;
            }
        }

        public void Refresh() {
            Collection = null;
            Get();
        }

        public override IEnumerable<T> Get() {
            if (Collection != null) {
                return Collection;
            }

            Collection = base.Get().ToList();
            return Collection;
        }

        public override IEnumerable<T> Get(Func<T, bool> predicate) {
            if (Collection == null) Get();
            return Collection.Where(predicate);
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
            Refresh(); // TODO: intelligent refresh
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            Refresh(); // TODO: intelligent refresh
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            Refresh(); // TODO: intelligent refresh
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.Update(filterExpression, update);
            Refresh(); // TODO: intelligent refresh
            List<T> items = Get(filterExpression.Compile()).ToList();
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, x.GetIdValue())));
        }

        public override async Task UpdateMany(Func<T, bool> predicate, UpdateDefinition<T> update) {
            await base.UpdateMany(predicate, update);
            Refresh(); // TODO: intelligent refresh
            List<T> items = Get(predicate).ToList();
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, x.GetIdValue())));
        }

        public override async Task Replace(T item) {
            await base.Replace(item);
            Refresh(); // TODO: intelligent refresh
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, item.GetIdValue()));
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            Refresh(); // TODO: intelligent refresh
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }

        public override async Task DeleteMany(Func<T, bool> predicate) {
            await base.DeleteMany(predicate);
            Refresh(); // TODO: intelligent refresh
            List<T> items = Get(predicate).ToList();
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, x.GetIdValue())));
        }
    }
}
