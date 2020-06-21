using System;
using System.Collections.Generic;

namespace UKSF.Api.Models.Personnel {
    public class Account : DatabaseObject {
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
        public List<string> rolePreferences = new List<string>();
        public ServiceRecordEntry[] serviceRecord = new ServiceRecordEntry[0];
        public AccountSettings settings = new AccountSettings();
        public string steamname;
        public HashSet<double> teamspeakIdentities;
        public string unitAssignment;
        public string unitsExperience;
    }
}
