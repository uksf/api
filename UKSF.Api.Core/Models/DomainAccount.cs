namespace UKSF.Api.Core.Models;

public class DomainAccount : MongoObject
{
    public Qualifications Qualifications { get; set; } = new();
    public bool Admin { get; set; }
    public Application Application { get; set; }
    public string ArmaExperience { get; set; }
    public string Background { get; set; }
    public string DiscordId { get; set; }
    public DateTime Dob { get; set; }
    public string Email { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public MembershipState MembershipState { get; set; } = MembershipState.UNCONFIRMED;
    public bool MilitaryExperience { get; set; }
    public string Nation { get; set; }
    public string Password { get; set; }
    public string Rank { get; set; }
    public string Reference { get; set; }
    public string RoleAssignment { get; set; }
    public List<string> RolePreferences { get; set; } = new();
    public List<ServiceRecordEntry> ServiceRecord { get; set; } = new();
    public AccountSettings Settings { get; set; } = new();
    public string Steamname { get; set; }
    public bool SuperAdmin { get; set; }
    public HashSet<int> TeamspeakIdentities { get; set; }
    public string UnitAssignment { get; set; }
    public string UnitsExperience { get; set; }
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
    public Application Application { get; set; }
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
    public string UnitAssignment { get; set; }
    public Unit UnitObject { get; set; }
    public List<Unit> UnitObjects { get; set; }
    public string UnitsExperience { get; set; }
}

public class BasicAccount
{
    public string DisplayName { get; set; }
    public string Id { get; set; }
}
