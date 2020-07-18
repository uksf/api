// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
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
                    LogWrapper.AuditLog("Migration utility successfully ran", "SERVER");
                } catch (Exception e) {
                    LogWrapper.Log(e);
                } finally {
                    VariablesWrapper.VariablesDataService().Update(KEY, "true");
                }
            }
        }

        // TODO: CHECK BEFORE RELEASE
        private static void ExecuteMigration() {
            IDataCollectionFactory dataCollectionFactory = ServiceWrapper.Provider.GetService<IDataCollectionFactory>();
            IDataCollection<OldAccount> oldDataCollection = dataCollectionFactory.CreateDataCollection<OldAccount>("accounts");
            List<OldAccount> oldAccounts = oldDataCollection.Get();

            IAccountDataService accountDataService = ServiceWrapper.Provider.GetService<IAccountDataService>();

            List<Account> newAccounts = new List<Account>();
            foreach (OldAccount oldAccount in oldAccounts) {
                Account newAccount = new Account {
                    id = oldAccount.id,
                    application = oldAccount.application,
                    armaExperience = oldAccount.armaExperience,
                    background = oldAccount.background,
                    discordId = oldAccount.discordId,
                    dob = oldAccount.dob,
                    email = oldAccount.email,
                    firstname = oldAccount.firstname,
                    lastname = oldAccount.lastname,
                    membershipState = oldAccount.membershipState,
                    militaryExperience = oldAccount.militaryExperience,
                    nation = oldAccount.nation,
                    password = oldAccount.password,
                    rank = oldAccount.rank,
                    reference = oldAccount.reference,
                    roleAssignment = oldAccount.roleAssignment,
                    serviceRecord = oldAccount.serviceRecord,
                    settings = oldAccount.settings,
                    steamname = oldAccount.steamname,
                    teamspeakIdentities = oldAccount.teamspeakIdentities,
                    unitAssignment = oldAccount.unitAssignment,
                    unitsExperience = oldAccount.unitsExperience
                };
                List<string> rolePreferences = new List<string>();
                if (oldAccount.nco) rolePreferences.Add("NCO");
                if (oldAccount.officer) rolePreferences.Add("Officer");
                if (oldAccount.aviation) rolePreferences.Add("Aviation");
                newAccount.rolePreferences = rolePreferences;

                newAccounts.Add(newAccount);
            }

            foreach (Account accountnewAccount in newAccounts) {
                accountDataService.Delete(accountnewAccount.id).Wait();
                accountDataService.Add(accountnewAccount).Wait();
            }
        }
    }

    public class OldAccount : DatabaseObject {
        public Application application;
        public string armaExperience;
        public string background;
        public string discordId;
        public DateTime dob;
        public string email;
        public string firstname;
        public string lastname;
        public MembershipState membershipState = MembershipState.UNCONFIRMED;
        public bool militaryExperience;
        public string nation;
        public string password;
        public string rank;
        public string reference;
        public string roleAssignment;
        public ServiceRecordEntry[] serviceRecord = new ServiceRecordEntry[0];
        public readonly AccountSettings settings = new AccountSettings();
        public string steamname;
        public HashSet<double> teamspeakIdentities;
        public string unitAssignment;
        public string unitsExperience;

        public bool aviation;
        public bool nco;
        public bool officer;
    }
}
