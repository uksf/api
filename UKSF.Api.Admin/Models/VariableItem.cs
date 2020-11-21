using UKSF.Api.Base.Models;

namespace UKSF.Api.Admin.Models {
    public record VariableItem : MongoObject {
        public object Item;
        public string Key;
    }
}
