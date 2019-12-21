using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Documents {
    public class Document {
        public DateTime created = DateTime.Now;
        public string directory;
        public DocumentPermissions editPermissions = new DocumentPermissions();

        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string id;

        public string name;
        public string originalAuthor;
        public List<DocumentVersion> versions = new List<DocumentVersion>();
        public DocumentPermissions viewPermissions = new DocumentPermissions();
    }
}
