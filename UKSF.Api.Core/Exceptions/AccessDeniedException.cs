namespace UKSF.Api.Core.Exceptions;

[Serializable]
public class AccessDeniedException : UksfException
{
    public AccessDeniedException() : base("Access denied", 403) { }
    public AccessDeniedException(string message) : base(message, 403) { }
}
