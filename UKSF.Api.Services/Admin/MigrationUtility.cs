// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Models.Utility;
using UKSF.Api.Services.Common;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Admin {
    public class MigrationUtility {
        private const string KEY = "MIGRATED";
        private readonly IHostEnvironment currentEnvironment;

        public MigrationUtility(IHostEnvironment currentEnvironment) => this.currentEnvironment = currentEnvironment;

        public void Migrate() {
            bool migrated = true;
            if (!currentEnvironment.IsDevelopment()) {
                string migratedString = VariablesWrapper.VariablesDataService().GetSingle(KEY).AsString();
                migrated = bool.Parse(migratedString);
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!migrated) {
                try {
                    ExecuteMigration();
                    LogWrapper.AuditLog("SERVER", "Migration utility successfully ran");
                } catch (Exception e) {
                    LogWrapper.Log(e);
                } finally {
                    VariablesWrapper.VariablesDataService().Update(KEY, "true");
                }
            }
        }

        // TODO: CHECK BEFORE RELEASE
        private static void ExecuteMigration() { }
    }
}
