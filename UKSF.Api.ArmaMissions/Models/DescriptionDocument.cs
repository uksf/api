namespace UKSF.Api.ArmaMissions.Models;

public class DescriptionDocument
{
    public List<string> Lines { get; set; } = [];
    public bool MissionPatchingIgnore { get; set; }
    public bool UseSimplePack { get; set; }
}
