using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.ArmaMissions.Models;

public class PatchData
{
    public List<DomainRank> Ranks { get; init; } = [];
    public List<PatchUnit> OrderedUnits { get; init; } = [];
}

public class PatchUnit
{
    public DomainUnit Source { get; init; }
    public string Callsign { get; init; }
    public List<PatchPlayer> Slots { get; init; } = [];
}

public class PatchPlayer
{
    public string DisplayName { get; init; }
    public string ObjectClass { get; init; }
    public string RoleAssignment { get; init; }
    public string Callsign { get; init; }
    public bool IsMedic { get; init; }
    public bool IsEngineer { get; init; }
    public DomainRank Rank { get; init; }
}
