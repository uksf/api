﻿using System;
using System.Threading.Tasks;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Base.Services;
using UKSF.Api.Integration.Instagram.Services;

namespace UKSF.Api.Integration.Instagram.ScheduledActions {
    public interface IActionInstagramImages : ISelfCreatingScheduledAction { }

    public class ActionInstagramImages : IActionInstagramImages {
        public const string ACTION_NAME = nameof(ActionInstagramImages);

        private readonly IClock clock;
        private readonly IInstagramService instagramService;
        private readonly ISchedulerService schedulerService;

        public ActionInstagramImages(IInstagramService instagramService, ISchedulerService schedulerService, IClock clock) {
            this.instagramService = instagramService;
            this.schedulerService = schedulerService;
            this.clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            Task unused = instagramService.CacheInstagramImages();
        }

        public async Task CreateSelf() {
            if (schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await schedulerService.CreateScheduledJob(clock.Today(), TimeSpan.FromMinutes(15), ACTION_NAME);
            }

            Run();
        }
    }
}