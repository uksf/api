using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Utility {
    public class ConfirmationCode : MongoObject {
        public DateTime timestamp = DateTime.UtcNow;
        public string value;
    }
}
