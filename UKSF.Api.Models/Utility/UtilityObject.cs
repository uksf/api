using System.Collections.Generic;

namespace UKSF.Api.Models.Utility {
    public class UtilityObject : MongoObject {
        public Dictionary<string, string> values = new Dictionary<string, string>();
    }
}
