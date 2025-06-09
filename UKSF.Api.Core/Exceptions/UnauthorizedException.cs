namespace UKSF.Api.Core.Exceptions;

[Serializable]
public class UnauthorizedException : UksfException
{
    public UnauthorizedException() : base("Unauthorized", 401) { }
    public UnauthorizedException(string message) : base(message, 401) { }
}
