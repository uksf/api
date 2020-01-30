using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface INotificationsDataService : IDataService<Notification, INotificationsDataService> {
        Task UpdateMany(FilterDefinition<Notification> filter, UpdateDefinition<Notification> update);
        Task DeleteMany(FilterDefinition<Notification> filter);
    }
}
