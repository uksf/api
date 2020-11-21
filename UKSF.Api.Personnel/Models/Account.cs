using System;
using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record Account : MongoObject {
        public Application Application;
        public string ArmaExperience;
        public string Background;
        public string DiscordId;
        public DateTime Dob;
        public string Email;
        public string Firstname;
        public string Lastname;
        public MembershipState MembershipState = MembershipState.UNCONFIRMED;
        public bool MilitaryExperience;
        public string Nation;
        public string Password;
        public string Rank;
        public string Reference;
        public string RoleAssignment;
        public List<string> RolePreferences = new();
        public List<ServiceRecordEntry> ServiceRecord = new();
        public AccountSettings Settings = new();
        public string Steamname;
        public HashSet<double> TeamspeakIdentities;
        public string UnitAssignment;
        public string UnitsExperience;
    }

    public record RosterAccount : MongoObject {
        public string Name;
        public string Nation;
        public string Rank;
        public string RoleAssignment;
        public string UnitAssignment;
    }

    public record PublicAccount : Account {
        public string DisplayName;
    }
}
