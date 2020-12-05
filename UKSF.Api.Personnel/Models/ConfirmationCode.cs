using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record ConfirmationCode : MongoObject {
        public string Value { get; set; }
    }
}
