using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class CreateAccount
{
    [Range(1, 31)]
    public int DobDay { get; set; }

    [Range(1, 12)]
    public int DobMonth { get; set; }

    [Range(1900, 2100)]
    public int DobYear { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string FirstName { get; set; }

    [Required]
    public string LastName { get; set; }

    [Required]
    public string Nation { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}
