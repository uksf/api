using System.Text.Json;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Modern;

/// <summary>
///     Handles parsing of JSON output from build processes
/// </summary>
public class JsonOutputParser
{
    /// <summary>
    ///     Attempts to parse JSON output from a message line
    /// </summary>
    /// <param name="message">The message to parse</param>
    /// <param name="parsedMessages">The parsed messages if successful</param>
    /// <returns>True if JSON was successfully parsed</returns>
    public bool TryParseJsonOutput(string message, out List<(string text, string color)> parsedMessages)
    {
        parsedMessages = [];

        if (string.IsNullOrEmpty(message) || message.Length <= 5 || !message.StartsWith("JSON"))
        {
            return false;
        }

        try
        {
            var parts = message.Split('}', '{');
            if (parts.Length < 2)
            {
                return false;
            }

            var json = $"{{{parts[1].Escape().Replace(@"\\n", "\\n")}}}";
            using var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            var text = GetJsonProperty(root, "message");
            var color = GetJsonProperty(root, "colour");

            parsedMessages.Add((text, color));

            // Handle additional parts
            parsedMessages.AddRange(parts.Skip(2).Where(x => !string.IsNullOrEmpty(x)).Select(extraPart => (extraPart, "")));

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetJsonProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) ? property.GetString() ?? "" : "";
    }
}
