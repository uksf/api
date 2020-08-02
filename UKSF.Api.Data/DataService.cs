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
    public abstract class DataService<T, TData> : DataServiceBase<T, TData>, IDataService<T, TData> {
        protected DataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataCollectionFactory, dataEventBus, collectionName) { }

        public override async Task Add(T data) {
            await base.Add(data);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.Update(filterExpression, update);
            List<string> ids = Get(filterExpression.Compile()).Select(x => x.GetIdValue()).ToList();
            ids.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, x)));
        }

        public override async Task UpdateMany(Func<T, bool> predicate, UpdateDefinition<T> update) {
            await base.UpdateMany(predicate, update);
            List<T> items = Get(predicate).ToList();
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, x.GetIdValue())));
        }

        public override async Task Replace(T item) {
            await base.Replace(item);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, item.GetIdValue()));
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }

        public override async Task DeleteMany(Func<T, bool> predicate) {
            await base.DeleteMany(predicate);
            List<T> items = Get(predicate).ToList(); // TODO: Evaluate performance impact of this presence check
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, x.GetIdValue())));
        }
    }
}
