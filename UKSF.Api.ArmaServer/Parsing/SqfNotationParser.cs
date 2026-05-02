using System.Globalization;
using System.Text;

namespace UKSF.Api.ArmaServer.Parsing;

/// <summary>
/// Parses Arma 3 SQF <c>str</c> output into a generic .NET object tree.
///
/// Output mapping:
/// <list type="bullet">
/// <item><description>SQF string <c>"..."</c> (with internal <c>"</c> doubled to <c>""</c>) → <see cref="string"/></description></item>
/// <item><description>SQF integer (no decimal/exponent) → <see cref="long"/></description></item>
/// <item><description>SQF number with decimal or exponent → <see cref="double"/></description></item>
/// <item><description>SQF <c>true</c>/<c>false</c> → <see cref="bool"/></description></item>
/// <item><description>SQF <c>any</c> (nil) → <c>null</c></description></item>
/// <item><description>SQF array <c>[v1,v2,...]</c> → <see cref="List{Object}"/></description></item>
/// <item><description>SQF HashMap (also serialised as <c>[[k,v],...]</c> by <c>str</c>) → <see cref="List{Object}"/> identical
/// to a pair-array; the caller distinguishes hashmap vs pair-array using its known schema. Use
/// <c>PersistenceConversionHelpers.ToDict</c> to coerce a pair-list to a <see cref="Dictionary{String,Object}"/>.</description></item>
/// </list>
///
/// <para>Strings preserve all literal characters (including backslash, newline, tab) verbatim; only the doubled-quote
/// is collapsed. Whitespace between tokens is tolerated.</para>
/// </summary>
public static class SqfNotationParser
{
    public static object Parse(string input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var cursor = new Cursor(input);
        cursor.SkipWhitespace();
        var value = ParseValue(cursor);
        cursor.SkipWhitespace();
        if (!cursor.AtEnd)
        {
            throw new FormatException($"Unexpected trailing content at position {cursor.Position}");
        }

        return value;
    }

    /// <summary>
    /// Parse and recursively normalise pair-lists to dictionaries.
    ///
    /// <para>SQF <c>HashMap</c> and an array of two-element pairs serialise identically via <c>str</c>
    /// (e.g. <c>[["a",1],["b",2]]</c>). The persistence schema never uses arrays of <c>[string, value]</c>
    /// pairs as positional data — every such shape in practice is a hashmap (top-level session, players,
    /// individual object/player records, ACE medical wound buckets). This method applies the heuristic
    /// "every element is a 2-element list whose first element is a string ⇒ dictionary" and converts
    /// matching lists to <see cref="Dictionary{String,Object}"/>. Empty lists are kept as empty lists
    /// (caller-side ToDict will produce an empty dict on demand).</para>
    /// </summary>
    public static object ParseAndNormalize(string input) => Normalize(Parse(input));

    private static object Normalize(object value)
    {
        if (value is List<object> list)
        {
            if (LooksLikeHashmap(list))
            {
                var dict = new Dictionary<string, object>(list.Count);
                foreach (var entry in list)
                {
                    var pair = (List<object>)entry;
                    dict[(string)pair[0]] = Normalize(pair[1]);
                }

                return dict;
            }

            for (var i = 0; i < list.Count; i++)
            {
                list[i] = Normalize(list[i]);
            }

            return list;
        }

        return value;
    }

    private static bool LooksLikeHashmap(List<object> list)
    {
        if (list.Count == 0) return false;
        foreach (var entry in list)
        {
            if (entry is not List<object> pair || pair.Count != 2 || pair[0] is not string) return false;
        }

        return true;
    }

    private static object ParseValue(Cursor cursor)
    {
        cursor.SkipWhitespace();
        if (cursor.AtEnd) throw new FormatException("Unexpected end of input");

        var c = cursor.Peek();
        return c switch
        {
            '['               => ParseArray(cursor),
            '"'               => ParseString(cursor),
            't' or 'f'        => ParseBool(cursor),
            'a'               => ParseAny(cursor),
            >= 'A' and <= 'Z' => ParseSideOrThrow(cursor),
            _                 => ParseNumber(cursor)
        };
    }

    private static readonly string[] SideKeywords =
    [
        "WEST", "EAST", "GUER", "CIV", "ENEMY", "FRIENDLY", "UNKNOWN", "EMPTY", "LOGIC"
    ];

    private static string ParseSideOrThrow(Cursor cursor)
    {
        // Defensive: SQF Side type str-emits as unquoted uppercase keyword (e.g. WEST, EAST).
        // Persistence data should serialise sides as quoted lowercase strings, but coerce here
        // for any leak path we haven't caught.
        foreach (var keyword in SideKeywords)
        {
            if (cursor.MatchLiteral(keyword)) return keyword.ToLowerInvariant();
        }

        throw new FormatException($"Unexpected token at position {cursor.Position}");
    }

    private static List<object> ParseArray(Cursor cursor)
    {
        cursor.Expect('[');
        var result = new List<object>();
        cursor.SkipWhitespace();
        if (cursor.Peek() == ']')
        {
            cursor.Advance();
            return result;
        }

        while (true)
        {
            cursor.SkipWhitespace();
            result.Add(ParseValue(cursor));
            cursor.SkipWhitespace();
            var next = cursor.Read();
            if (next == ']') return result;
            if (next != ',') throw new FormatException($"Expected ',' or ']' at position {cursor.Position - 1}, got '{next}'");
        }
    }

    private static string ParseString(Cursor cursor)
    {
        cursor.Expect('"');
        var builder = new StringBuilder();
        while (true)
        {
            if (cursor.AtEnd) throw new FormatException("Unterminated string literal");
            var c = cursor.Read();
            if (c == '"')
            {
                if (!cursor.AtEnd && cursor.Peek() == '"')
                {
                    // Doubled-quote escape — consume second quote, append a literal "
                    cursor.Advance();
                    builder.Append('"');
                    continue;
                }

                return builder.ToString();
            }

            builder.Append(c);
        }
    }

    private static bool ParseBool(Cursor cursor)
    {
        if (cursor.MatchLiteral("true")) return true;
        if (cursor.MatchLiteral("false")) return false;
        throw new FormatException($"Expected 'true' or 'false' at position {cursor.Position}");
    }

    private static object ParseAny(Cursor cursor)
    {
        if (cursor.MatchLiteral("any")) return null;
        throw new FormatException($"Expected 'any' at position {cursor.Position}");
    }

    private static object ParseNumber(Cursor cursor)
    {
        var start = cursor.Position;
        var hasFraction = false;
        var hasExponent = false;

        if (cursor.Peek() == '-') cursor.Advance();

        while (!cursor.AtEnd)
        {
            var c = cursor.Peek();
            if (c >= '0' && c <= '9')
            {
                cursor.Advance();
            }
            else if (c == '.' && !hasFraction && !hasExponent)
            {
                hasFraction = true;
                cursor.Advance();
            }
            else if ((c == 'e' || c == 'E') && !hasExponent)
            {
                hasExponent = true;
                cursor.Advance();
                if (!cursor.AtEnd && (cursor.Peek() == '+' || cursor.Peek() == '-'))
                {
                    cursor.Advance();
                }
            }
            else
            {
                break;
            }
        }

        var text = cursor.Slice(start);
        if (text.Length == 0 || text == "-")
        {
            throw new FormatException($"Expected number at position {start}");
        }

        if (!hasFraction && !hasExponent)
        {
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        throw new FormatException($"Invalid number '{text}' at position {start}");
    }

    private sealed class Cursor(string input)
    {
        public int Position { get; private set; }

        public bool AtEnd => Position >= input.Length;

        public char Peek() => input[Position];

        public char Read()
        {
            if (AtEnd) throw new FormatException($"Unexpected end of input at position {Position}");
            var c = input[Position];
            Position++;
            return c;
        }

        public void Advance() => Position++;

        public void Expect(char expected)
        {
            if (AtEnd) throw new FormatException($"Expected '{expected}' but reached end of input");
            if (input[Position] != expected) throw new FormatException($"Expected '{expected}' at position {Position}, got '{input[Position]}'");
            Position++;
        }

        public bool MatchLiteral(string literal)
        {
            if (Position + literal.Length > input.Length) return false;
            for (var i = 0; i < literal.Length; i++)
            {
                if (input[Position + i] != literal[i]) return false;
            }

            Position += literal.Length;
            return true;
        }

        public string Slice(int start) => input.Substring(start, Position - start);

        public void SkipWhitespace()
        {
            while (!AtEnd && char.IsWhiteSpace(input[Position]))
            {
                Position++;
            }
        }
    }
}
