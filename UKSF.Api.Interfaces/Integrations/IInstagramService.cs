using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Models.Integrations;

namespace UKSF.Api.Interfaces.Integrations {
    public interface IInstagramService {
        Task RefreshAccessToken();
        Task CacheInstagramImages();
        IEnumerable<InstagramImage> GetImages();
    }
}
