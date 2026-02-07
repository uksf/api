using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class AssignRecruiterRequest
{
    [Required]
    public string NewRecruiter { get; set; }
}
