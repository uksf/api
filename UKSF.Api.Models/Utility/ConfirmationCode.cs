using System;

namespace UKSF.Api.Models.Utility {
    public class ConfirmationCode : MongoObject {
        public DateTime timestamp = DateTime.UtcNow;
        public string value;
    }
}
