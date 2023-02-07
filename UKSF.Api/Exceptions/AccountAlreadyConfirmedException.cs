using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class AccountAlreadyConfirmedException : UksfException
{
    public AccountAlreadyConfirmedException() : base("Account email has already been confirmed", 400) { }
}
