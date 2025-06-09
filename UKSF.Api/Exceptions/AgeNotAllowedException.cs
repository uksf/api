using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class AgeNotAllowedException : UksfException
{
    public AgeNotAllowedException() : base("Application cannot be accepted due to age requirements. Speak to ELCOM", 400) { }
}
