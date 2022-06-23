using System;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Auth.Exceptions
{
    [Serializable]
    public class TokenRefreshFailedException : UksfException
    {
        public TokenRefreshFailedException(string message) : base(message, 401) { }
    }
}
