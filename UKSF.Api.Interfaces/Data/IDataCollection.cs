using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace UKSF.Api.Interfaces.Data {
    public interface IDataCollection<T> {
        List<T> Get();
        List<T> Get(Func<T, bool> predicate);
        T GetSingle(string id);
        T GetSingle(Func<T, bool> predicate);
        Task AddAsync(T data);
        Task UpdateAsync(string id, UpdateDefinition<T> update);
        Task UpdateManyAsync(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update);
        Task ReplaceAsync(string id, T value);
        Task DeleteAsync(string id);
        Task DeleteManyAsync(Expression<Func<T, bool>> predicate);
    }
}
