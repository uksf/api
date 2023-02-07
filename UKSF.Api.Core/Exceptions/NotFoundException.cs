namespace UKSF.Api.Core.Exceptions;

[Serializable]
public class NotFoundException : UksfException
{
    public NotFoundException(string message) : base(message, 404) { }
}
