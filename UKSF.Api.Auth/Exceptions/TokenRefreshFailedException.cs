using System;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Auth.Exceptions
{
    [Serializable]
    public class TokenRefreshFailedException : UksfException
    {
        public TokenRefreshFailedException() : base("Failed to refresh token", 401) { }
    }
}
