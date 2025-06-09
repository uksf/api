using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class InvalidConfirmationCodeException : UksfException
{
    public InvalidConfirmationCodeException() : base("Confirmation code was invalid or expired. Please try again", 400) { }
}
