using System.Collections;
using System.Globalization;
using System.Text;

namespace UKSF.Api.ArmaServer.Parsing;

/// <summary>
/// Inverse of <see cref="SqfNotationParser"/>: walks a generic .NET object tree and emits
/// the canonical Arma 3 SQF <c>str</c> output. Used both for round-trip equivalence checks
/// and for shipping API → game payloads as raw SQF (avoiding game-side JSON parsing cost).
///
/// <para>Mapping (mirrors the parser):</para>
/// <list type="bullet">
/// <item><description><c>null</c> → <c>any</c></description></item>
/// <item><description><see cref="bool"/> → <c>true</c>/<c>false</c></description></item>
/// <item><description>integer numerics → invariant integer literal (e.g. <c>42</c>)</description></item>
/// <item><description>floating numerics → invariant decimal literal (e.g. <c>3.14</c>)</description></item>
/// <item><description><see cref="string"/> → <c>"..."</c> with internal <c>"</c> doubled</description></item>
/// <item><description><see cref="IDictionary{TKey,TValue}"/> with string keys → <c>[[k,v],...]</c> (matches BIS HashMap <c>str</c> output)</description></item>
/// <item><description>any other <see cref="IEnumerable"/> → <c>[a,b,c]</c></description></item>
/// </list>
/// </summary>
public static class SqfNotationWriter
{
    public static string Write(object value)
    {
        var sb = new StringBuilder();
        WriteValue(sb, value);
        return sb.ToString();
    }

    private static void WriteValue(StringBuilder sb, object value)
    {
        switch (value)
        {
            case null:
                sb.Append("any");
                return;
            case bool b:
                sb.Append(b ? "true" : "false");
                return;
            case string s:
                WriteString(sb, s);
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                sb.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                return;
            case float f:
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            case double d:
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            case decimal dec:
                sb.Append(dec.ToString(CultureInfo.InvariantCulture));
                return;
            case IDictionary dict:
                WriteDictionary(sb, dict);
                return;
            case IEnumerable enumerable:
                WriteEnumerable(sb, enumerable);
                return;
            default:
                sb.Append('"').Append(value).Append('"');
                return;
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            if (c == '"') sb.Append("\"\"");
            else sb.Append(c);
        }

        sb.Append('"');
    }

    private static void WriteDictionary(StringBuilder sb, IDictionary dict)
    {
        sb.Append('[');
        var first = true;
        foreach (DictionaryEntry entry in dict)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('[');
            WriteValue(sb, entry.Key);
            sb.Append(',');
            WriteValue(sb, entry.Value);
            sb.Append(']');
        }

        sb.Append(']');
    }

    private static void WriteEnumerable(StringBuilder sb, IEnumerable enumerable)
    {
        sb.Append('[');
        var first = true;
        foreach (var item in enumerable)
        {
            if (!first) sb.Append(',');
            first = false;
            WriteValue(sb, item);
        }

        sb.Append(']');
    }
}
