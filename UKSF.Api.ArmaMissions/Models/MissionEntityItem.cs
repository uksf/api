namespace UKSF.Api.ArmaMissions.Models;

public class MissionEntityItem
{
    public static double Position { get; set; } = 10;
    public static double CuratorPosition { get; set; } = 0.5;
    public string DataType { get; set; }
    public bool IsPlayable { get; set; }
    public MissionEntity MissionEntity { get; set; }
    public List<string> RawMissionEntities { get; set; } = new();
    public List<string> RawMissionEntityItem { get; set; } = new();
    public string Type { get; set; }
}
