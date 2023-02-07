using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.ArmaServer.Exceptions;

[Serializable]
public class ServerInfrastructureException : UksfException
{
    public ServerInfrastructureException(string message, int statusCode) : base(message, statusCode) { }
}
