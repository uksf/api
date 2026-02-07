using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class TeamspeakCode
{
    [Required]
    public string Code { get; set; }
}
