namespace UKSF.Api.Shared.Models;

public class CommandRequestLoa : CommandRequest
{
    public bool Emergency { get; set; }
    public DateTime End { get; set; }
    public bool Late { get; set; }
    public DateTime Start { get; set; }
}
