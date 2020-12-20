using System;

namespace UKSF.Api.Shared.Exceptions {
    public class UksfException : Exception {
        public UksfException(int statusCode, string message) : base(message) => StatusCode = statusCode;

        public int StatusCode { get; }
    }
}
