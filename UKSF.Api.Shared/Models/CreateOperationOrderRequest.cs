namespace UKSF.Api.Shared.Models;

public class CreateOperationOrderRequest
{
    public DateTime End { get; set; }
    public int Endtime { get; set; }
    public string Map { get; set; }
    public string Name { get; set; }
    public DateTime Start { get; set; }
    public int Starttime { get; set; }
    public string Type { get; set; }
}
