namespace UKSF.Api.Services;

public interface IBotDetectionService
{
    bool IsBot(string userAgent);
}

public class BotDetectionService : IBotDetectionService
{
    private static readonly string[] BotPatterns =
    [
        "googlebot", "bingbot", "baiduspider", "yandexbot", "duckduckbot", "slurp",
        "facebookexternalhit", "twitterbot", "linkedinbot", "discordbot", "whatsapp",
        "applebot", "bytespider", "gptbot", "claudebot", "semrushbot", "ahrefsbot",
        "dotbot", "petalbot", "mj12bot"
    ];

    public bool IsBot(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return true;
        }

        var lower = userAgent.ToLowerInvariant();
        return BotPatterns.Any(pattern => lower.Contains(pattern));
    }
}
