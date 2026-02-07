using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class RequestPasswordReset
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
