using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.Data {
    public class DataEventBus : EventBus<DataEventModel>, IDataEventBus { }
}
