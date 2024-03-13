using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Models;

public enum UksfLogLevel
{
    Debug,
    Info,
    Error,
    Warning
}

public class BasicLog : MongoObject
{
    protected BasicLog()
    {
        Level = UksfLogLevel.Info;
        Timestamp = DateTime.UtcNow;
    }

    public BasicLog(string text) : this()
    {
        Message = text;
    }

    public BasicLog(string text, UksfLogLevel uksfLogLevel) : this()
    {
        Message = text;
        Level = uksfLogLevel;
    }

    public BasicLog(Exception exception) : this()
    {
        Message = exception.GetCompleteString();
        Level = UksfLogLevel.Error;
    }

    public BasicLog(string message, Exception exception) : this()
    {
        Message = $"{message}:\n{exception.GetCompleteString()}";
        Level = UksfLogLevel.Error;
    }

    [BsonRepresentation(BsonType.String)]
    public UksfLogLevel Level { get; set; }

    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
