using System;
using System.Text.RegularExpressions;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class VoiceSlug
{
    // Lowercase; runs of non-alphanumerics collapse to a single underscore; trim leading/trailing underscores.
    private static string Slugify(string input)
    {
        var lowered = (input ?? string.Empty).ToLowerInvariant();
        return Regex.Replace(lowered, "[^a-z0-9]+", "_").Trim('_');
    }

    public static string Derive(string displayName, string moodOf, string moodLabel)
    {
        if (!string.IsNullOrWhiteSpace(moodOf))
        {
            var label = Slugify(moodLabel);
            if (label.Length == 0)
            {
                throw new ArgumentException("Mood label produced an empty slug");
            }

            return $"{moodOf}_{label}";
        }

        var slug = Slugify(displayName);
        if (slug.Length == 0)
        {
            throw new ArgumentException("Display name produced an empty slug");
        }

        return slug;
    }
}
