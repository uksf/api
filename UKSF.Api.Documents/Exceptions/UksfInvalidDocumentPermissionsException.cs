using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Documents.Exceptions {
    public class UksfInvalidDocumentPermissionsException : UksfException {
        public UksfInvalidDocumentPermissionsException(string message) : base(400, message) { }

        public UksfInvalidDocumentPermissionsException() : this("Invalid document permissions object") { }
    }
}
