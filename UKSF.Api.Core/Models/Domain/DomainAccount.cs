using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class DomainAccount : MongoObject
{
    public Qualifications Qualifications { get; set; } = new();
    public bool Admin { get; set; }
    public DomainApplication Application { get; set; }
    public string ArmaExperience { get; set; }
    public string Background { get; set; }
    public string DiscordId { get; set; }
    public DateTime Dob { get; set; }
    public string Email { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public MembershipState MembershipState { get; set; } = MembershipState.Unconfirmed;
    public bool MilitaryExperience { get; set; }
    public string Nation { get; set; }
    public string Password { get; set; }
    public string Rank { get; set; }
    public string Reference { get; set; }
    public string RoleAssignment { get; set; }
    public List<string> RolePreferences { get; set; } = [];
    public List<ServiceRecordEntry> ServiceRecord { get; set; } = [];
    public AccountSettings Settings { get; set; } = new();
    public string Steamname { get; set; }
    public bool SuperAdmin { get; set; }
    public HashSet<int> TeamspeakIdentities { get; set; }
    public string UnitAssignment { get; set; }
    public string UnitsExperience { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Trainings { get; set; } = [];

    public bool IsMember()
    {
        return MembershipState is MembershipState.Member;
    }

    public bool IsCandidate()
    {
        return MembershipState is MembershipState.Confirmed && Rank == "Candidate";
    }
}

public class AccountSettings
{
    public bool NotificationsEmail { get; set; } = true;
    public bool NotificationsTeamspeak { get; set; } = true;
    public bool Sr1Enabled { get; set; } = true;
}

public class Qualifications
{
    public bool Engineer { get; set; }
    public bool Medic { get; set; }
}

public class RosterAccount : MongoObject
{
    public string Name { get; set; }
    public string Nation { get; set; }
    public string Rank { get; set; }
    public string RoleAssignment { get; set; }
    public string UnitAssignment { get; set; }
}

public class Account
{
    public bool Admin { get; set; }
    public DomainApplication Application { get; set; }
    public string ArmaExperience { get; set; }
    public string Background { get; set; }
    public string DiscordId { get; set; }
    public string DisplayName { get; set; }
    public DateTime Dob { get; set; }
    public string Email { get; set; }
    public string Firstname { get; set; }
    public string Id { get; set; }
    public string Lastname { get; set; }
    public MembershipState MembershipState { get; set; }
    public bool MilitaryExperience { get; set; }
    public string Nation { get; set; }
    public Qualifications Qualifications { get; set; }
    public string Rank { get; set; }
    public Rank RankObject { get; set; }
    public string Reference { get; set; }
    public string RoleAssignment { get; set; }
    public Role RoleObject { get; set; }
    public List<string> RolePreferences { get; set; }
    public List<ServiceRecordEntry> ServiceRecord { get; set; }
    public AccountSettings Settings { get; set; }
    public string Steamname { get; set; }
    public HashSet<int> TeamspeakIdentities { get; set; }
    public List<Training> Trainings { get; set; }
    public string UnitAssignment { get; set; }
    public UnitTreeNodeDto UnitObject { get; set; }
    public List<UnitTreeNodeDto> UnitObjects { get; set; }
    public string UnitsExperience { get; set; }
}

public class BasicAccount
{
    public string DisplayName { get; set; }
    public string Id { get; set; }
}
