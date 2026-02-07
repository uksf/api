using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class SteamCodeRequest
{
    [Required]
    public string Code { get; set; }
}
