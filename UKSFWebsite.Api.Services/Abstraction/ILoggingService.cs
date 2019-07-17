using System;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Logging;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ILoggingService {
        Task LogAsync(Exception exception);
        Task LogAsync(BasicLogMessage log);
    }
}
