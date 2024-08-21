using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Models.Domain;

public enum UksfLogLevel
{
    Debug,
    Info,
    Error,
    Warning
}

public class DomainBasicLog : MongoObject
{
    protected DomainBasicLog()
    {
        Level = UksfLogLevel.Info;
        Timestamp = DateTime.UtcNow;
    }

    public DomainBasicLog(string text) : this()
    {
        Message = text;
    }

    public DomainBasicLog(string text, UksfLogLevel uksfLogLevel) : this()
    {
        Message = text;
        Level = uksfLogLevel;
    }

    public DomainBasicLog(Exception exception) : this()
    {
        Message = exception.GetCompleteString();
        Level = UksfLogLevel.Error;
    }

    public DomainBasicLog(string message, Exception exception) : this()
    {
        Message = $"{message}:\n{exception.GetCompleteString()}";
        Level = UksfLogLevel.Error;
    }

    [BsonRepresentation(BsonType.String)]
    public UksfLogLevel Level { get; set; }

    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
