using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.ArmaMissions.Models;

public class MissionUnit
{
    public string Callsign { get; set; }
    public List<MissionPlayer> Members { get; set; } = new();
    public Dictionary<string, MissionPlayer> Roles { get; set; } = new();
    public DomainUnit SourceUnit { get; set; }
}
