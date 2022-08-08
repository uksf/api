namespace UKSF.Api.ArmaMissions.Models;

public class MissionEntity
{
    public int ItemsCount { get; set; }
    public List<MissionEntityItem> MissionEntityItems { get; set; } = new();
}
