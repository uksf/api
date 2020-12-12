using System;
using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public class Account : MongoObject {
        public AccountSettings Settings = new();
        public MembershipState MembershipState = MembershipState.UNCONFIRMED;
        public List<string> RolePreferences = new();
        public List<ServiceRecordEntry> ServiceRecord = new();
        public bool Admin;
        public Application Application;
        public string ArmaExperience;
        public string Background;
        public string DiscordId;
        public DateTime Dob;
        public string Email;
        public string Firstname;
        public string Lastname;
        public bool MilitaryExperience;
        public string Nation;
        public string Password;
        public string Rank;
        public string Reference;
        public string RoleAssignment;
        public string Steamname;
        public HashSet<double> TeamspeakIdentities;
        public string UnitAssignment;
        public string UnitsExperience;
    }

    public class RosterAccount : MongoObject {
        public string Name;
        public string Nation;
        public string Rank;
        public string RoleAssignment;
        public string UnitAssignment;
    }

    public class PublicAccount : Account {
        public string DisplayName;
    }
}
