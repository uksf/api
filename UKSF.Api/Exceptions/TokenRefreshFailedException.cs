using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class TokenRefreshFailedException(string message) : UksfException(message, 401);
