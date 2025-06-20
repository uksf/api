namespace UKSF.Api.Core.Exceptions;

[Serializable]
public class BadRequestException : UksfException
{
    public BadRequestException(string message) : base(message, 400) { }

    public BadRequestException() : this("Bad request") { }
}
