using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class LoginCredentials
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}
