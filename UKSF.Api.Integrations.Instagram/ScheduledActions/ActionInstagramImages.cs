using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Integrations.Instagram.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Integrations.Instagram.ScheduledActions {
    public interface IActionInstagramImages : ISelfCreatingScheduledAction { }

    public class ActionInstagramImages : IActionInstagramImages {
        private const string ACTION_NAME = nameof(ActionInstagramImages);

        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly IInstagramService _instagramService;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;

        public ActionInstagramImages(ISchedulerContext schedulerContext, IInstagramService instagramService, ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock) {
            _schedulerContext = schedulerContext;
            _instagramService = instagramService;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            Task unused = _instagramService.CacheInstagramImages();
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today(), TimeSpan.FromMinutes(15), ACTION_NAME);
            }

            Run();
        }
    }
}
