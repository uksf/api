using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class ChangePasswordRequest
{
    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}
