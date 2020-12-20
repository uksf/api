using System.Net;

namespace UKSF.Api.Shared.Exceptions {
    public class UksfNotFoundException : UksfException {
        public UksfNotFoundException(string message) : base((int) HttpStatusCode.NotFound, message) { }
    }
}
