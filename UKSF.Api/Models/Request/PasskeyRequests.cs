using System.ComponentModel.DataAnnotations;
using Fido2NetLib;

namespace UKSF.Api.Models.Request;

public class PasskeyLoginRequest
{
    [Required]
    public string FlowId { get; set; }

    [Required]
    public AuthenticatorAssertionRawResponse Credential { get; set; }
}

public class PasskeyRegistrationRequest
{
    [Required]
    public string FlowId { get; set; }

    [Required]
    public AuthenticatorAttestationRawResponse Credential { get; set; }
}

public class CreateAccountPasskeyOptionsRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string FirstName { get; set; }

    [Required]
    public string LastName { get; set; }
}
