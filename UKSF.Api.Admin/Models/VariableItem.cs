using UKSF.Api.Base.Models;

namespace UKSF.Api.Admin.Models {
    public record VariableItem : MongoObject {
        public object Item { get; set; }
        public string Key { get; set; }
    }
}
