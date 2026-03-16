namespace UKSF.Api.Core.Models.Domain;

public class MissionPatchSettings
{
    public string ObjectClassOverride { get; set; }
    public string CallsignOverride { get; set; }
    public bool MergeIntoParent { get; set; }
    public bool KeepWhenEmpty { get; set; }
    public int MinSlots { get; set; }
    public string FillerName { get; set; }
    public string FillerRank { get; set; }
}
