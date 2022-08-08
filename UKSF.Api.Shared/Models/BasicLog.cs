using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Shared.Models;

public enum LogLevel
{
    DEBUG,
    INFO,
    ERROR,
    WARNING
}

public class BasicLog : MongoObject
{
    [BsonRepresentation(BsonType.String)]
    public LogLevel Level { get; set; }

    public string Message { get; set; }
    public DateTime Timestamp { get; set; }

    protected BasicLog()
    {
        Level = LogLevel.INFO;
        Timestamp = DateTime.UtcNow;
    }

    public BasicLog(string text) : this()
    {
        Message = text;
    }

    public BasicLog(string text, LogLevel logLevel) : this()
    {
        Message = text;
        Level = logLevel;
    }

    public BasicLog(Exception exception) : this()
    {
        Message = exception.GetBaseException().ToString();
        Level = LogLevel.ERROR;
    }
}
