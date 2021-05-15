using System;

namespace UKSF.Api.Shared.Exceptions
{
    [Serializable]
    public class UksfException : Exception
    {
        protected UksfException(string message, int statusCode, Exception inner = null) : base(message, inner)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}
