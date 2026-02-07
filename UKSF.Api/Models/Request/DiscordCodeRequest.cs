using System.ComponentModel.DataAnnotations;

namespace UKSF.Api.Models.Request;

public class DiscordCodeRequest
{
    [Required]
    public string Code { get; set; }
}
