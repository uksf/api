using System;
using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models
{
    public class DomainAccount : MongoObject
    {
        public bool SuperAdmin;
        public bool Admin;
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
        public Qualifications Qualifications = new();
        public string Rank;
        public string Reference;
        public string RoleAssignment;
        public List<string> RolePreferences = new();
        public List<ServiceRecordEntry> ServiceRecord = new();
        public readonly AccountSettings Settings = new();
        public string Steamname;
        public HashSet<int> TeamspeakIdentities;
        public string UnitAssignment;
        public string UnitsExperience;
    }

    public class Qualifications
    {
        public bool Medic;
        public bool Engineer;
    }

    public class RosterAccount : MongoObject
    {
        public string Name;
        public string Nation;
        public string Rank;
        public string RoleAssignment;
        public string UnitAssignment;
    }

    public class Account
    {
        public bool Admin;
        public Application Application;
        public string ArmaExperience;
        public string Background;
        public string DiscordId;
        public string DisplayName;
        public DateTime Dob;
        public string Email;
        public string Firstname;
        public string Id;
        public string Lastname;
        public MembershipState MembershipState;
        public bool MilitaryExperience;
        public string Nation;
        public Qualifications Qualifications;
        public string Rank;
        public Rank RankObject;
        public string Reference;
        public string RoleAssignment;
        public Role RoleObject;
        public List<string> RolePreferences;
        public List<ServiceRecordEntry> ServiceRecord;
        public AccountSettings Settings;
        public string Steamname;
        public HashSet<int> TeamspeakIdentities;
        public string UnitAssignment;
        public Unit UnitObject;
        public List<Unit> UnitObjects;
        public string UnitsExperience;
    }

    public class BasicAccount
    {
        public string DisplayName;
        public string Id;
    }
}
