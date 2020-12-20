using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Documents.Models {
    public class ContextDocumentMetadata : MongoObject {
        public DateTime CreatedUtc;
        [BsonRepresentation(BsonType.ObjectId)] public string CreatorId;
        public DateTime LastUpdatedUtc;
        public string Name;
        public string Path;
    }

    public class DocumentMetadata : ContextDocumentMetadata {
        public bool CanView;
        public bool CanEdit;
    }
}
