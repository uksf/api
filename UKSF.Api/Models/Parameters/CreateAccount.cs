namespace UKSF.Api.Models.Parameters;

public class CreateAccount
{
    public string DobDay { get; set; }
    public string DobMonth { get; set; }
    public string DobYear { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Nation { get; set; }
    public string Password { get; set; }
}
