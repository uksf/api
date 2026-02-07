using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class CreateLoaRequest
{
    [Required]
    public string Reason { get; set; }

    [Required]
    public DateTime Start { get; set; }

    [Required]
    public DateTime End { get; set; }

    public bool Emergency { get; set; }
    public bool Late { get; set; }
}
