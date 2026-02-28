namespace UKSF.Api.Core.Models.Domain;

public class MissionPatchSettings
{
    public int MaxSlots { get; set; }
    public string FillerName { get; set; }
    public string FillerRank { get; set; }
    public bool IsPermanent { get; set; }
    public bool AggregateIntoParent { get; set; }
    public bool Pruned { get; set; }
    public bool IsPilotUnit { get; set; }
    public string ForcedObjectClass { get; set; }
}
