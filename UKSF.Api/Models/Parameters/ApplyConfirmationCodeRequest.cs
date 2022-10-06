namespace UKSF.Api.Models.Parameters;

public class ApplyConfirmationCodeRequest
{
    public string Email { get; set; }
    public string Code { get; set; }
}
