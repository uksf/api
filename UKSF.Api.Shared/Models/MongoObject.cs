﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Shared.Models;

public class MongoObject
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
}