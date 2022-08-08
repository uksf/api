using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Personnel.Exceptions;

[Serializable]
public class InvalidConfirmationCodeException : UksfException
{
    public InvalidConfirmationCodeException() : base("Confirmation code was invalid or expired. Please try again", 400) { }
}
