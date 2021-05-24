using System;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Auth.Exceptions
{
    [Serializable]
    public class LoginFailedException : UksfException
    {
        public LoginFailedException(string message) : base(message, 401) { }
    }
}
