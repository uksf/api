﻿// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Services.Common;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Admin {
    public class MigrationUtility {
        private const string KEY = "MIGRATED";
        private readonly IHostEnvironment currentEnvironment;
        private readonly IMongoDatabase database;

        public MigrationUtility(IMongoDatabase database, IHostEnvironment currentEnvironment) {
            this.database = database;
            this.currentEnvironment = currentEnvironment;
        }

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
        private static void ExecuteMigration() {
            IUnitsService unitsService = ServiceWrapper.ServiceProvider.GetService<IUnitsService>();
            IRolesService rolesService = ServiceWrapper.ServiceProvider.GetService<IRolesService>();
            List<Role> roles = rolesService.Data().Get(x => x.roleType == RoleType.UNIT);

            foreach (Unit unit in unitsService.Data().Get()) {
                Dictionary<string, string> unitRoles = unit.roles;
                int originalCount = unit.roles.Count;
                foreach ((string key, string _) in unitRoles.ToList()) {
                    if (roles.All(x => x.name != key)) {
                        unitRoles.Remove(key);
                    }
                }

                if (roles.Count != originalCount) {
                    unitsService.Data().Update(unit.id, Builders<Unit>.Update.Set(x => x.roles, unitRoles)).Wait();
                }
            }
        }
    }

//    public class OldLoa {
//        public bool approved;
//        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
//        public bool late;
//        public string reason;
//        public string emergency;
//        [BsonRepresentation(BsonType.ObjectId)] public string recipient;
//        public DateTime start;
//        public DateTime end;
//        public DateTime submitted;
//    }
}