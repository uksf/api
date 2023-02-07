namespace UKSF.Api.Models.Request;

public class ApplyConfirmationCodeRequest
{
    public string Email { get; set; }
    public string Code { get; set; }
}
