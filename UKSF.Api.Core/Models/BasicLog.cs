using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Models;

public enum UksfLogLevel
{
    DEBUG,
    INFO,
    ERROR,
    WARNING
}

public class BasicLog : MongoObject
{
    protected BasicLog()
    {
        Level = UksfLogLevel.INFO;
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
        Level = UksfLogLevel.ERROR;
    }

    public BasicLog(string message, Exception exception) : this()
    {
        Message = $"{message}\n{exception.GetCompleteString()}";
        Level = UksfLogLevel.ERROR;
    }

    [BsonRepresentation(BsonType.String)]
    public UksfLogLevel Level { get; set; }

    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
