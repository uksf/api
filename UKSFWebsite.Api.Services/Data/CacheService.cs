using System.Collections.Generic;

namespace UKSFWebsite.Api.Services.Data {
    public class CacheService {
        private readonly List<dynamic> services = new List<dynamic>();

        public void AddService(dynamic service) => services.Add(service);

        public void InvalidateCaches() {
            foreach (dynamic service in services) {
                service.Refresh();
            }
        }
    }
}
