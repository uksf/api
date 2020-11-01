using System;
using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
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
        public List<ServiceRecordEntry> serviceRecord = new List<ServiceRecordEntry>();
        public AccountSettings settings = new AccountSettings();
        public string steamname;
        public HashSet<double> teamspeakIdentities;
        public string unitAssignment;
        public string unitsExperience;
    }

    public class RosterAccount : DatabaseObject {
        public string name;
        public string rank;
        public string roleAssignment;
        public string unitAssignment;
        public string nation;
    }

    public class PublicAccount : Account {
        public string displayName;
    }
}
