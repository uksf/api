using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Events;

namespace UKSFWebsite.Api.Interfaces.Data {
    public interface IDataService<T, TData> : IDataEventBacker<TData> {
        List<T> Get();
        List<T> Get(Func<T, bool> predicate);
        T GetSingle(string id);
        T GetSingle(Func<T, bool> predicate);
        Task Add(T data);
        Task Update(string id, string fieldName, object value);
        Task Update(string id, UpdateDefinition<T> update);
        Task Delete(string id);
    }
}
