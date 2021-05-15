using System;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Exceptions
{
    [Serializable]
    public class InvalidLoaScopeException : UksfException
    {
        public InvalidLoaScopeException(string scope) : base($"'{scope}' is an invalid LOA scope", 400) { }
    }
}
