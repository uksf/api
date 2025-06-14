using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class AccountAlreadyExistsException : UksfException
{
    public AccountAlreadyExistsException() : base("An account with that email already exists", 409) { }
}
