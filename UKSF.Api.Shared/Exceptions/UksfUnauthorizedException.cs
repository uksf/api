using System.Net;

namespace UKSF.Api.Shared.Exceptions {
    public class UksfUnauthorizedException : UksfException {
        public UksfUnauthorizedException(string message) : base((int) HttpStatusCode.Unauthorized, message) { }

        public UksfUnauthorizedException() : this("Unauthorized") { }
    }
}
