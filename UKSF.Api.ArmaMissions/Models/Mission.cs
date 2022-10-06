namespace UKSF.Api.ArmaMissions.Models;

public class Mission
{
    public Mission(string path)
    {
        Path = path;
        DescriptionPath = $"{Path}/description.ext";
        SqmPath = $"{Path}/mission.sqm";
    }

    public static int NextId { get; set; }
    public List<string> DescriptionLines { get; set; }
    public string DescriptionPath { get; set; }
    public int MaxCurators { get; set; }
    public MissionEntity MissionEntity { get; set; }
    public string Path { get; set; }
    public int PlayerCount { get; set; }
    public List<string> RawEntities { get; set; }
    public List<string> SqmLines { get; set; }
    public string SqmPath { get; set; }
}
