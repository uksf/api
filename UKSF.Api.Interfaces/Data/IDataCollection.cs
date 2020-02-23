using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace UKSF.Api.Interfaces.Data {
    public interface IDataCollection {
        Task AssertCollectionExistsAsync<T>();
        List<T> Get<T>();
        List<T> Get<T>(Func<T, bool> predicate);
        T GetSingle<T>(string id);
        T GetSingle<T>(Func<T, bool> predicate);
        Task AddAsync<T>(T data);
        Task UpdateAsync<T>(string id, UpdateDefinition<T> update);
        Task UpdateManyAsync<T>(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update);
        Task ReplaceAsync<T>(string id, T value);
        Task DeleteAsync<T>(string id);
        Task DeleteManyAsync<T>(Expression<Func<T, bool>> predicate);
    }
}
