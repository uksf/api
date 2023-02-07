namespace UKSF.Api.Models.Request;

public class CreateAccount
{
    public int DobDay { get; set; }
    public int DobMonth { get; set; }
    public int DobYear { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Nation { get; set; }
    public string Password { get; set; }
}
