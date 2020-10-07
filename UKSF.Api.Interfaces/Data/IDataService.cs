using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace UKSF.Api.Interfaces.Data {
    public interface IDataService<T> {
        IEnumerable<T> Get();
        IEnumerable<T> Get(Func<T, bool> predicate);
        T GetSingle(string id);
        T GetSingle(Func<T, bool> predicate);
        Task Add(T item);
        Task Update(string id, string fieldName, object value);
        Task Update(string id, UpdateDefinition<T> update);
        Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
        Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
        Task Replace(T item);
        Task Delete(string id);
        Task Delete(T item);
        Task DeleteMany(Expression<Func<T, bool>> filterExpression);
    }
}
