using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Personnel {
    public class Account {
        public Application application;
        public string armaExperience;
        public bool aviation;
        public string background;
        public string discordId;
        public DateTime dob;
        public string email;
        public string firstname;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string lastname;
        public MembershipState membershipState = MembershipState.UNCONFIRMED;
        public bool militaryExperience;
        public string nation;
        public bool nco;
        public bool officer;
        public string password;
        public string rank;
        public string reference;
        public string roleAssignment;
        public ServiceRecordEntry[] serviceRecord = new ServiceRecordEntry[0];
        public AccountSettings settings = new AccountSettings();
        public string steamname;
        public HashSet<string> teamspeakIdentities;
        public string unitAssignment;
        public string unitsExperience;
    }
}
