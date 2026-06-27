namespace UKSF.Api.ArmaServer.Models;

public enum MissionFileState
{
    Missing,
    Present
}

public class OpDto
{
    public DomainOp Op { get; set; }
    public MissionFileState MissionFileState { get; set; }
}
