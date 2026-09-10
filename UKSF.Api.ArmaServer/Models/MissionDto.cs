namespace UKSF.Api.ArmaServer.Models;

public enum MissionFileState
{
    Missing,
    Present
}

public class MissionDto
{
    public DomainMission Mission { get; set; }
    public MissionFileState MissionFileState { get; set; }
}
