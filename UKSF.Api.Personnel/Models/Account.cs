using System;
using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record Account : MongoObject {
        public AccountSettings Settings { get; } = new();
        public MembershipState MembershipState { get; set; } = MembershipState.UNCONFIRMED;
        public List<string> RolePreferences { get; set; } = new();
        public List<ServiceRecordEntry> ServiceRecord { get; set; } = new();
        public bool Admin { get; set; }
        public Application Application { get; set; }
        public string ArmaExperience { get; set; }
        public string Background { get; set; }
        public string DiscordId { get; set; }
        public DateTime Dob { get; set; }
        public string Email { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public bool MilitaryExperience { get; set; }
        public string Nation { get; set; }
        public string Password { get; set; }
        public string Rank { get; set; }
        public string Reference { get; set; }
        public string RoleAssignment { get; set; }
        public string Steamname { get; set; }
        public HashSet<double> TeamspeakIdentities { get; set; }
        public string UnitAssignment { get; set; }
        public string UnitsExperience { get; set; }
    }

    public record RosterAccount : MongoObject {
        public string Name { get; set; }
        public string Nation { get; set; }
        public string Rank { get; set; }
        public string RoleAssignment { get; set; }
        public string UnitAssignment { get; set; }
    }

    public record PublicAccount : Account {
        public string DisplayName { get; set; }
    }
}
