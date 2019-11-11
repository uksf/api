using System.Collections.Generic;

namespace UKSFWebsite.Api.Services.Utility {
    public class DataCacheService {
        private readonly List<dynamic> dataServices = new List<dynamic>();

        public void AddDataService(dynamic dataService) => dataServices.Add(dataService);

        public void InvalidateDataCaches() {
            foreach (dynamic dataService in dataServices) {
                dataService.Refresh();
            }
        }
    }
}
