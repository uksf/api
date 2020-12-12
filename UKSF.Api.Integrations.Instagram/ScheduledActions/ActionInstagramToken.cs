using System;
using System.Threading.Tasks;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Integrations.Instagram.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Integrations.Instagram.ScheduledActions {
    public interface IActionInstagramToken : ISelfCreatingScheduledAction { }

    public class ActionInstagramToken : IActionInstagramToken {
        private const string ACTION_NAME = nameof(ActionInstagramToken);

        private readonly IClock _clock;
        private readonly IInstagramService _instagramService;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;

        public ActionInstagramToken(ISchedulerContext schedulerContext, IInstagramService instagramService, ISchedulerService schedulerService, IClock clock) {
            _schedulerContext = schedulerContext;
            _instagramService = instagramService;
            _schedulerService = schedulerService;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            Task unused = _instagramService.RefreshAccessToken();
        }

        public async Task CreateSelf() {
            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(45), TimeSpan.FromDays(45), ACTION_NAME);
            }
        }
    }
}
