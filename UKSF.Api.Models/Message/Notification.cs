﻿using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Message {
    public class Notification : MongoObject {
        public string icon;
        public string link;
        public string message;
        [BsonRepresentation(BsonType.ObjectId)] public string owner;
        public bool read = false;
        public DateTime timestamp = DateTime.Now;
    }
}