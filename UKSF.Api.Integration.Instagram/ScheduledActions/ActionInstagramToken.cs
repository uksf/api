using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Integration.Instagram.Services;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Integration.Instagram.ScheduledActions {
    public interface IActionInstagramToken : ISelfCreatingScheduledAction { }

    public class ActionInstagramToken : IActionInstagramToken {
        private const string ACTION_NAME = nameof(ActionInstagramToken);

        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly IInstagramService _instagramService;
        private readonly ISchedulerService _schedulerService;

        public ActionInstagramToken(IInstagramService instagramService, ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock) {
            _instagramService = instagramService;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            Task unused = _instagramService.RefreshAccessToken();
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(45), TimeSpan.FromDays(45), ACTION_NAME);
            }
        }
    }
}
