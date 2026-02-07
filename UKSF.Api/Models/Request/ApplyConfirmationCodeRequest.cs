using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class ApplyConfirmationCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Code { get; set; }
}
