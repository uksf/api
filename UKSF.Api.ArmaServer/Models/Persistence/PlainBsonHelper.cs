namespace UKSF.Api.ArmaServer.Models.Persistence;

using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

public static class PlainBsonHelper
{
    public static void WriteValue(IBsonWriter writer, object value)
    {
        switch (value)
        {
            case null:                                      writer.WriteNull(); break;
            case bool b:                                    writer.WriteBoolean(b); break;
            case int i:                                     writer.WriteInt32(i); break;
            case long l:                                    writer.WriteInt64(l); break;
            case double d:                                  writer.WriteDouble(d); break;
            case string s:                                  writer.WriteString(s); break;
            case List<object> list:                         WriteArray(writer, list); break;
            case object[] arr:                              WriteArray(writer, arr); break;
            case Dictionary<string, object> dict:           WriteDocument(writer, dict); break;
            case Dictionary<string, List<object>> dictList: WriteDictionaryOfLists(writer, dictList); break;
            default:
                if (value is IConvertible)
                {
                    try
                    {
                        writer.WriteInt64(Convert.ToInt64(value));
                        return;
                    }
                    catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException) { }

                    try
                    {
                        writer.WriteDouble(Convert.ToDouble(value));
                        return;
                    }
                    catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException) { }
                }

                writer.WriteString(value.ToString());
                break;
        }
    }

    public static void WriteArray(IBsonWriter writer, IEnumerable<object> items)
    {
        writer.WriteStartArray();
        foreach (var item in items) WriteValue(writer, item);
        writer.WriteEndArray();
    }

    public static void WriteDocument(IBsonWriter writer, Dictionary<string, object> dict)
    {
        writer.WriteStartDocument();
        foreach (var kvp in dict)
        {
            writer.WriteName(kvp.Key);
            WriteValue(writer, kvp.Value);
        }

        writer.WriteEndDocument();
    }

    public static void WriteDictionaryOfLists(IBsonWriter writer, Dictionary<string, List<object>> dict)
    {
        writer.WriteStartDocument();
        foreach (var kvp in dict)
        {
            writer.WriteName(kvp.Key);
            WriteArray(writer, kvp.Value);
        }

        writer.WriteEndDocument();
    }

    public static object ReadValue(IBsonReader reader)
    {
        return reader.CurrentBsonType switch
        {
            BsonType.Null     => ReadNull(reader),
            BsonType.Boolean  => reader.ReadBoolean(),
            BsonType.Int32    => reader.ReadInt32(),
            BsonType.Int64    => reader.ReadInt64(),
            BsonType.Double   => reader.ReadDouble(),
            BsonType.String   => reader.ReadString(),
            BsonType.Array    => ReadArray(reader),
            BsonType.Document => ReadDocument(reader),
            _                 => ReadFallback(reader)
        };
    }

    public static List<object> ReadArray(IBsonReader reader)
    {
        reader.ReadStartArray();
        var list = new List<object>();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            list.Add(ReadValue(reader));
        }

        reader.ReadEndArray();
        return list;
    }

    public static Dictionary<string, object> ReadDocument(IBsonReader reader)
    {
        reader.ReadStartDocument();
        var dict = new Dictionary<string, object>();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            var name = reader.ReadName();
            dict[name] = ReadValue(reader);
        }

        reader.ReadEndDocument();
        return dict;
    }

    private static object ReadNull(IBsonReader reader)
    {
        reader.ReadNull();
        return null;
    }

    private static object ReadFallback(IBsonReader reader)
    {
        var value = BsonValueSerializer.Instance.Deserialize(BsonDeserializationContext.CreateRoot(reader));
        return value?.ToString();
    }
}
