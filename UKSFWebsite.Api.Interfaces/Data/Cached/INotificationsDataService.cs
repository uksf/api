using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface INotificationsDataService : IDataService<Notification, INotificationsDataService> {
        Task UpdateMany(FilterDefinition<Notification> filter, UpdateDefinition<Notification> update);
        Task DeleteMany(FilterDefinition<Notification> filter);
    }
}
