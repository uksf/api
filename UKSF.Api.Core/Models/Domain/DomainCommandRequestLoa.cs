namespace UKSF.Api.Core.Models.Domain;

public class DomainCommandRequestLoa : DomainCommandRequest
{
    public bool Emergency { get; set; }
    public DateTime End { get; set; }
    public bool Late { get; set; }
    public DateTime Start { get; set; }
}
