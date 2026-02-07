using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class ChangeName
{
    [Required]
    public string FirstName { get; set; }

    [Required]
    public string LastName { get; set; }
}
