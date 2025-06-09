namespace UKSF.Api.Models.Request;

public class CreateLoaRequest
{
    public string Reason { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Emergency { get; set; }
    public bool Late { get; set; }
}
