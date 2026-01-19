namespace UKSF.Api.Core.Exceptions;

[Serializable]
public class BadRequestException(string message) : UksfException(message, 400)
{
    public BadRequestException() : this("Bad request") { }
}
